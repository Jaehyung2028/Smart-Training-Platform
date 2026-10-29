using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SmartTraining
{
    // 서버가 확정한 진행 상태를 콘텐츠 씬 표시로 연결합니다. 로비와 네트워크는 유지하고 콘텐츠만 추가 로드합니다.
    // 콘텐츠 로딩 자체는 로컬 표시 작업이며 시작 권한·준비 검사·이동 좌표의 최종 판단은 기존 서버가 담당합니다.
    public sealed class ContentSceneSession : MonoBehaviour
    {
        public const string ScenePath = "Assets/SmartTraining/Scenes/TrainingContent.unity";
        private ApiLobbyScreen screen;
        private Coroutine transition;
        private bool requested;
        private Scene previousScene;
        private ContentAssetScope contentAssets;
        private string contentMessage = "";
        private string loadedRoomId = "";
        private FirstPersonCameraController firstPersonCamera;

        private Camera PlayerCamera => firstPersonCamera != null ? firstPersonCamera.ViewCamera : null;


        private ModelViewer modelViewer;
        private VideoContentPlayer videoViewer;
        private ScenarioContent scenarioViewer;
        private readonly List<Camera> previousCameras = new();
        private readonly List<AudioListener> previousListeners = new();
        private readonly ClientPreferences preferences = new();
        private ContentHudController hud;
        private ContentParticipantState[] participantStates = System.Array.Empty<ContentParticipantState>();

        private bool InputBlocked => hud?.BlocksGameplayInput == true;


        private string loadError = "";

        public bool SceneReady { get; private set; }

        public bool IsActive => requested || transition != null || SceneReady;


        private void OnEnable()
        {
            preferences.Load();
            preferences.ApplyAudioVolume();
            ApplyPreferences();
            hud?.EnableInput();
        }


        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                hud?.SetMenuOpen(true);
            }
        }


        private void OnDisable()
        {
            hud?.DisableInput();
            StopSession();
        }


        // Controller가 매 프레임 원하는 상태를 전달합니다. 로딩 도중 퇴장해도 완료 후 즉시 언로드합니다.
        internal void Synchronize(ApiLobbyScreen controller, bool running)
        {
            screen = controller;
            requested = running;
            if (transition == null && (requested != SceneReady || HasDifferentRoom()) && (loadError.Length == 0 || !requested))
            {
                transition = StartCoroutine(ReconcileScene());
            }

            if (!requested || (loadedRoomId.Length > 0 && loadedRoomId != screen.UiActiveRoomId))
            {
                // 다운로드 자체가 취소되지 않더라도, 완료 후 이전 방의 오브젝트가 생성되는 것은 막습니다.
                contentAssets?.Dispose();
            }

            if (!requested)
            {
                hud?.CloseOptions();
                loadError = "";
                ReleaseCursor();
            }

            RefreshHud();
        }


        // 한 번에 한 전환만 실행합니다. 로딩 중 바뀐 요청도 완료 후 반복문에서 다시 확인합니다.
        private IEnumerator ReconcileScene()
        {
            // 첫 yield는 StartCoroutine 반환 전에 transition이 다시 덮어써지는 것을 방지합니다.
            yield return null;
            while (requested != SceneReady || HasDifferentRoom())
            {
                if (requested && !SceneReady)
                {
                    yield return LoadContentScene();
                    if (!SceneReady)
                    {
                        break;
                    }
                }
                else
                {
                    yield return UnloadContentScene();
                }
            }

            transition = null;
        }


        private bool HasDifferentRoom()
        {
            return SceneReady && loadedRoomId != screen.UiActiveRoomId;
        }


        private IEnumerator LoadContentScene()
        {
            loadedRoomId = screen.UiActiveRoomId;
            var definition = screen.UiRoomContent;
            contentMessage = "";
            EnsureHud();
            hud.SetMenuOpen(false);
            hud.ConfigureContentActions(definition?.contentType);
            if (!Application.CanStreamedLevelBeLoaded(ScenePath))
            {
                loadError = "콘텐츠 씬을 찾을 수 없습니다. 빌드 씬 설정을 확인해주세요.";
                Debug.LogError(loadError);
                yield break;
            }

            previousScene = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                loadError = "콘텐츠 씬을 불러오지 못했습니다.";
                yield break;
            }

            if (requested && loadedRoomId == screen.UiActiveRoomId)
            {
                if (string.IsNullOrWhiteSpace(definition?.assetId))
                {
                    contentMessage = "기본 콘텐츠 프리팹이 아직 등록되지 않았습니다. catalog.json의 assetId를 지정해주세요.";
                }
                else
                {
                    contentMessage = "기본 콘텐츠 프리팹을 불러오고 있습니다…";
                    contentAssets = new ContentAssetScope();
                    var container = new GameObject("Content Resources", typeof(ContentResourceContext));
                    SceneManager.MoveGameObjectToScene(container, scene);
                    container.GetComponent<ContentResourceContext>().Initialize(contentAssets, definition.contentType);
                    yield return contentAssets.Instantiate(definition.assetId, container.transform, instance =>
                    {
                        contentMessage = "";
                        if (definition.contentType == "model")
                        {
                            // 모델도 공통 3D 공간에 고정 배치합니다. 관찰은 기존 네트워크 캐릭터로 이동하며 진행합니다.
                            modelViewer = instance.GetComponent<ModelViewer>() ?? instance.AddComponent<ModelViewer>();
                            if (!modelViewer.TryInitialize(out var modelError))
                            {
                                loadError = modelError;
                                modelViewer = null;
                            }
                        }
                        else if (definition.contentType == "video")
                        {
                            videoViewer = instance.GetComponent<VideoContentPlayer>();
                            if (videoViewer == null)
                            {
                                loadError = "영상 프리팹에 VideoContentPlayer를 연결해주세요.";
                            }
                            else if (!videoViewer.TryInitialize(out var videoError))
                            {
                                loadError = videoError;
                                videoViewer = null;
                            }
                        }
                        else if (definition.contentType == "scenario")
                        {
                            var scenario = instance.GetComponent<ScenarioContent>();
                            scenarioViewer = scenario;
                            if (scenario == null)
                            {
                                loadError = "시나리오 프리팹에 ScenarioContent를 연결해주세요.";
                            }
                            else if (!scenario.TryInitialize(out var scenarioError))
                            {
                                loadError = scenarioError;
                            }
                            else
                            {
                                contentMessage = "";
                            }
                        }
                    }, message => loadError = message);
                }
            }

            // 로딩 도중 대기 복귀·퇴장이 요청되거나 방이 바뀌면 카메라를 만들지 않고 정리합니다.
            SceneReady = true;
            if (requested && loadedRoomId == screen.UiActiveRoomId)
            {
                SceneManager.SetActiveScene(scene);
                BeginFirstPerson(scene);
            }
        }


        private IEnumerator UnloadContentScene()
        {
            EndFirstPerson();
            var unloading = RestorePreviousScene();
            if (unloading != null)
            {
                yield return unloading;
            }
        }


        // 정상 복귀와 GameObject 비활성화 모두 같은 경로로 씬을 정리합니다.
        // 비활성화 시에는 코루틴을 시작할 수 없으므로 비동기 작업 자체를 반환합니다.
        private AsyncOperation RestorePreviousScene()
        {
            SceneReady = false;
            SetPlayerPresentation(false);
            hud?.ClearContent();
            modelViewer = null;
            videoViewer = null;
            scenarioViewer = null;
            contentAssets?.Dispose();
            contentAssets = null;
            contentMessage = "";
            loadedRoomId = "";
            if (previousScene.IsValid() && previousScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousScene);
            }

            var scene = SceneManager.GetSceneByPath(ScenePath);
            return scene.IsValid() && scene.isLoaded ? SceneManager.UnloadSceneAsync(scene) : null;
        }


        private void BeginFirstPerson(Scene scene)
        {
            foreach (var camera in Camera.allCameras)
            {
                if (camera.enabled)
                {
                    previousCameras.Add(camera);
                    camera.enabled = false;
                }
            }

            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (listener.enabled)
                {
                    previousListeners.Add(listener);
                    listener.enabled = false;
                }
            }

            var cameraObject = new GameObject(
                "Local First Person Camera",
                typeof(Camera),
                typeof(AudioListener),
                typeof(FirstPersonCameraController)
            );
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            firstPersonCamera = cameraObject.GetComponent<FirstPersonCameraController>();
            firstPersonCamera.Bind(
                screen.UiLocalPlayer.transform,
                () => requested &&
                SceneReady &&
                !InputBlocked &&
                screen.UiLocalPlayer != null &&
                !screen.UiProcessing &&
                !screen.UiLocalPlayer.PhasePending
            );
            firstPersonCamera.Configure(preferences.FieldOfView, preferences.Sensitivity, preferences.InvertY);
            UguiViewUtility.ClearSelection(screen.transform);
            ReleaseCursor();
        }


        private void LateUpdate()
        {
            if (!requested || !SceneReady || firstPersonCamera == null || screen.UiLocalPlayer == null)
            {
                return;
            }

            // 공통 카메라의 시선 결과만 네트워크 참가자에 전달합니다. 회전 계산은 카메라 모듈에 둡니다.
            screen.UiLocalPlayer.SetViewYaw(firstPersonCamera.Yaw);
            // 카메라 갱신은 공통 컴포넌트가 담당하고, 이곳에서는 콘텐츠 상호작용에 결과만 전달합니다.
            modelViewer?.UpdateInteraction(PlayerCamera, screen.UiLocalPlayer, firstPersonCamera.CanInteract);
            scenarioViewer?.UpdateInteraction(PlayerCamera, screen.UiLocalPlayer, firstPersonCamera.CanInteract);
        }


        // 마우스로 보는 수평 방향을 기준으로 입력을 회전시킵니다. 실제 이동 속도·범위는 서버가 검사합니다.
        internal Vector2 ReadMovement()
        {
            if (!SceneReady ||
                !requested ||
                InputBlocked ||
                PlayerCamera == null ||
                UguiViewUtility.IsEditingText ||
                screen.UiLocalPlayer?.PhasePending == true)
            {
                return Vector2.zero;
            }

            return firstPersonCamera.ToWorldMovement(PlayerMovementInput.Read());
        }


        private void EnsureHud()
        {
            if (hud != null)
            {
                return;
            }

            var font = screen.GetComponentInChildren<Text>(true)?.font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hud = new ContentHudController(
                transform,
                font,
                preferences,
                new ContentHudActions
                {
                    SettingsChanged = ApplyPreferences,
                    ReturnToWaiting = () => screen.UiTogglePhase(),
                    LeaveRoom = () => screen.UiLeaveRoom(),
                    SendChat = (text, completed) => screen.UiSendContentChat(text, completed)
                }
            );
        }


        private void SetPlayerPresentation(bool visible)
        {
            if (screen == null)
            {
                return;
            }

            foreach (var participant in screen.UiRoomPlayers)
            {
                participant.SetPresentationVisible(visible, PlayerCamera, scenarioViewer?.ParticipantRoleLabel(participant.OwnerClientId) ?? "");
            }
        }


        private void RefreshHud()
        {
            SetPlayerPresentation(requested && SceneReady && !HasDifferentRoom());
            if (hud == null)
            {
                return;
            }

            videoViewer?.Bind(screen.UiLocalPlayer);
            scenarioViewer?.Bind(screen.UiLocalPlayer);
            hud.Refresh(new ContentHudState
            {
                Active = IsActive,
                Requested = requested,
                SceneReady = SceneReady,
                Busy = screen.UiProcessing,
                CanInteract = !screen.UiProcessing && screen.UiLocalPlayer != null && !screen.UiLocalPlayer.PhasePending,
                IsMaster = screen.UiLocalPlayer?.IsRoomMaster == true,
                CanReturn = screen.UiRoomCanPhase,
                CanLeave = screen.UiRoomCanAct,
                Error = loadError,
                ContentMessage = contentMessage,
                Model = modelViewer,
                Video = videoViewer,
                Scenario = scenarioViewer,
                Progress = screen.UiLocalPlayer != null ? screen.UiLocalPlayer.GetComponent<TrainingProgress>() : null,
                RoomId = screen.UiActiveRoomId,
                LocalUserId = screen.UiSelfId,
                ChatMessages = screen.UiContentRoomMessages,
                ChatStatus = screen.UiContentChatStatus,
                CanSendChat = screen.UiCanSendContentChat,
                Participants = GetParticipantStates(),
                Camera = PlayerCamera
            });
        }


        private ContentParticipantState[] GetParticipantStates()
        {
            var players = screen.UiRoomPlayers;
            if (participantStates.Length != players.Length)
            {
                participantStates = new ContentParticipantState[players.Length];
            }
            for (int index = 0; index < players.Length; index++)
            {
                var player = players[index];
                participantStates[index] = new ContentParticipantState(
                    player.UserId, player.UserName, player.IsOwner, player.IsRoomMaster, player.ConnectionStatusLabel);
            }
            return participantStates;
        }


        // 설정 UI에서 전달한 값을 연결된 카메라에 적용합니다. 저장·취소는 HUD 컨트롤러가 담당합니다.
        private void ApplyPreferences()
        {
            if (firstPersonCamera != null)
            {
                firstPersonCamera.Configure(preferences.FieldOfView, preferences.Sensitivity, preferences.InvertY);
            }
        }


        private static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }


        private void EndFirstPerson()
        {
            hud?.CloseOptions();
            ReleaseCursor();
            if (PlayerCamera != null)
            {
                PlayerCamera.gameObject.SetActive(false);
                Destroy(PlayerCamera.gameObject);
                firstPersonCamera = null;
            }

            foreach (var camera in previousCameras)
            {
                if (camera != null)
                {
                    camera.enabled = true;
                }
            }

            previousCameras.Clear();
            foreach (var listener in previousListeners)
            {
                if (listener != null)
                {
                    listener.enabled = true;
                }
            }

            previousListeners.Clear();
        }


        internal void StopSession()
        {
            requested = false;
            SetPlayerPresentation(false);
            hud?.EndSession();
            contentAssets?.Dispose();
            EndFirstPerson();
            if (!gameObject.activeInHierarchy)
            {
                RestorePreviousScene();
            }
            else if (transition == null && SceneReady)
            {
                transition = StartCoroutine(ReconcileScene());
            }
        }


        private void OnDestroy()
        {
            contentAssets?.Dispose();
            SceneReady = false;
            hud?.Dispose();
            hud = null;
            modelViewer = null;
            EndFirstPerson();
        }
    }
}
