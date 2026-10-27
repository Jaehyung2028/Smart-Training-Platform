using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SmartTraining
{
    // 서버가 확정한 진행 상태를 콘텐츠 씬 표시로 연결합니다. 로비와 네트워크는 유지하고 콘텐츠만 추가 로드합니다.
    // 콘텐츠 로딩 자체는 로컬 표시 작업이며 시작 권한·준비 검사·이동 좌표의 최종 판단은 기존 서버가 담당합니다.
    public sealed class ContentSceneSession : MonoBehaviour
    {
        public const string ScenePath = "Assets/SmartTraining/Scenes/TrainingContent.unity";

        private const float LookSensitivity = 0.12f;

        private const float PitchLimit = 85f;

        private ApiLobbyScreen screen;

        private Coroutine transition;

        private bool requested;

        private Scene previousScene;

        private ContentAssetScope contentAssets;

        private string contentMessage = "";

        private string loadedRoomId = "";

        private Camera playerCamera;

        private ModelViewer modelViewer;

        private float inspectionZoom = 1f;

        private readonly List<Camera> previousCameras = new();

        private readonly List<AudioListener> previousListeners = new();

        private GameObject hud;

        private GameObject pauseMenu, menuCard;

        private ContentOptionsPanel optionsPanel;

        private ContentActionMenu actionMenu;

        private ModelPartDetailsPanel partDetails;
        private ModelStepPanel stepPanel;

        private ModelPartListPanel partList;

        private readonly ContentPreferences preferences = new();

        private bool optionsOpen;

        private Text notice;

        private GameObject modelControlPanel;

        private Text modelControlStatus;

        private Button resumeButton, menuButton;

        private Button returnButton, leaveButton;

        private Font font;

        private float yaw, pitch;

        private bool menuOpen;

        private InputAction menuAction;

        private int lookInputStartsAtFrame;

        private string loadError = "";

        public static bool SceneReady { get; private set; }

        public bool IsActive => requested || transition != null || SceneReady;

        private void OnEnable()
        {
            preferences.Load();
            ApplyPreferences();
            // 이벤트로 받으면 짧게 누른 Esc 입력도 프레임 사이에 놓치지 않습니다.
            menuAction = new InputAction("Content Menu", InputActionType.Button, "<Keyboard>/escape");
            menuAction.performed += ToggleMenu;
            menuAction.Enable();
        }

        private void ToggleMenu(InputAction.CallbackContext context)
        {
            if (requested && hud != null)
            {
                if (optionsOpen)
                {
                    CloseOptions();
                }
                else if (actionMenu?.IsOpen == true)
                {
                    actionMenu.Close();
                }
                else if (partList?.IsOpen == true)
                {
                    partList.Close();
                }
                else
                {
                    SetMenuOpen(!menuOpen);
                }
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                menuOpen = true;
            }
        }

        private void OnDisable()
        {
            menuAction?.Dispose();
            menuAction = null;
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
                CloseOptions();
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
            SetMenuOpen(false);
            EnsureHud();
            ConfigureContentActions(definition?.contentType);
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
                    yield return contentAssets.Instantiate(definition.assetId, container.transform,
                        instance =>
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
                        },
                        message => loadError = message);
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
            actionMenu?.ClearActions();
            partList?.Clear();
            modelViewer = null;
            inspectionZoom = 1f;
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

            var cameraObject = new GameObject("Local First Person Camera", typeof(Camera), typeof(AudioListener));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            playerCamera = cameraObject.GetComponent<Camera>();
            playerCamera.nearClipPlane = 0.05f;
            playerCamera.fieldOfView = preferences.FieldOfView;
            playerCamera.clearFlags = CameraClearFlags.SolidColor;
            playerCamera.backgroundColor = new Color32(148, 163, 184, 255);
            yaw = pitch = 0;
            lookInputStartsAtFrame = Time.frameCount + 2;
            UguiViewUtility.ClearSelection(screen.transform);
            ReleaseCursor();
        }

        private void LateUpdate()
        {
            if (!requested || !SceneReady || playerCamera == null || screen.UiLocalPlayer == null)
            {
                return;
            }

            // 기능 메뉴는 조작을 중지하지 않습니다. Esc 설정 메뉴와 상태 전환 중에만 조작을 막습니다.
            bool canLook = !menuOpen && Application.isFocused && !screen.UiProcessing && !screen.UiLocalPlayer.PhasePending;
            if (modelViewer != null)
            {
                bool overUi = UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
                // 휠 버튼을 누른 동안만 응시합니다. 이동·모델 크기는 유지하고 화각을 25% 좁힙니다.
                bool inspecting = canLook && !overUi && Mouse.current?.middleButton.isPressed == true;
                float targetZoom = inspecting ? 0.75f : 1f;
                if (canLook)
                {
                    // 누르기·놓기 모두 부드럽게 전환하되, 메뉴·포커스 상실 시에는 즉시 기본 시야로 복귀합니다.
                    inspectionZoom = Mathf.Lerp(inspectionZoom, targetZoom, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
                    if (Mathf.Abs(inspectionZoom - targetZoom) < 0.001f)
                    {
                        inspectionZoom = targetZoom;
                    }
                }
                else
                {
                    inspectionZoom = 1f;
                }
                playerCamera.fieldOfView = preferences.FieldOfView * inspectionZoom;
            }

            // 관리 버튼을 언제든 누를 수 있게 커서는 유지하고, 오른쪽 버튼을 누른 동안만 시점을 회전합니다.
            if (canLook && Time.frameCount >= lookInputStartsAtFrame && Mouse.current?.rightButton.isPressed == true)
            {
                var delta = Mouse.current.delta.ReadValue();
                float sensitivity = LookSensitivity * preferences.Sensitivity;
                float vertical = preferences.InvertY ? delta.y : -delta.y;
                yaw = Mathf.Repeat(yaw + delta.x * sensitivity, 360);
                pitch = Mathf.Clamp(pitch + vertical * sensitivity, -PitchLimit, PitchLimit);
            }

            playerCamera.transform.SetPositionAndRotation(
                screen.UiLocalPlayer.transform.position + Vector3.up * 0.65f,
                Quaternion.Euler(pitch, yaw, 0)
            );
            if (modelViewer != null)
            {
                modelViewer.UpdateInteraction(playerCamera, screen.UiLocalPlayer, canLook);
            }
        }

        // 마우스로 보는 수평 방향을 기준으로 입력을 회전시킵니다. 실제 이동 속도·범위는 서버가 검사합니다.
        internal Vector2 ReadMovement()
        {
            if (!SceneReady || !requested || menuOpen || playerCamera == null || screen.UiLocalPlayer?.PhasePending == true)
            {
                return Vector2.zero;
            }

            var input = RoomMovementInput.Read();
            var world = Quaternion.Euler(0, yaw, 0) * new Vector3(input.x, 0, input.y);
            return new Vector2(world.x, world.z);
        }

        private void RefreshHud()
        {
            if (hud == null)
            {
                return;
            }

            hud.SetActive(IsActive);
            notice.text = GetNotice();
            bool showModelStatus = requested && SceneReady && modelViewer != null && !menuOpen && loadError.Length == 0;
            modelControlPanel.SetActive(showModelStatus);
            partDetails.Refresh(modelViewer, showModelStatus);
            stepPanel.Refresh(modelViewer, playerCamera, showModelStatus,
                !screen.UiProcessing && screen.UiLocalPlayer != null && !screen.UiLocalPlayer.PhasePending);
            partList.Refresh(modelViewer, showModelStatus, !screen.UiProcessing && screen.UiLocalPlayer != null && !screen.UiLocalPlayer.PhasePending);
            if (showModelStatus)
            {
                modelControlStatus.text = modelViewer.GetControlStatus();
                modelControlStatus.color = modelViewer.IsLocalManipulator ? new Color32(74, 222, 128, 255) :
                    modelViewer.IsBeingManipulated ? new Color32(251, 191, 36, 255) : Color.white;
            }
            pauseMenu.SetActive(requested && (menuOpen || loadError.Length > 0));
            menuCard.SetActive(!optionsOpen);
            optionsPanel.Root.SetActive(optionsOpen);
            menuButton.interactable = requested;
            actionMenu.Refresh(requested && SceneReady, !menuOpen && loadError.Length == 0 && !screen.UiProcessing &&
                screen.UiLocalPlayer != null && !screen.UiLocalPlayer.PhasePending);
            resumeButton.interactable = SceneReady && !screen.UiProcessing;
            returnButton.gameObject.SetActive(screen.UiLocalPlayer?.IsRoomMaster == true);
            returnButton.interactable = screen.UiRoomCanPhase;
            leaveButton.interactable = screen.UiRoomCanAct;
        }

        private string GetNotice()
        {
            if (loadError.Length > 0)
            {
                return loadError;
            }

            if (!SceneReady)
            {
                return contentMessage.Length > 0 ? contentMessage : "콘텐츠 씬을 준비하고 있습니다…";
            }

            if (!menuOpen && contentMessage.Length > 0)
            {
                return contentMessage;
            }

            if (!menuOpen && modelViewer != null)
            {
                return "WASD 이동 · 우클릭 드래그 시점 · 부품 클릭 설명 · 좌클릭 드래그 회전 · 휠 버튼 누르는 동안 응시 · Esc 메뉴";
            }

            return menuOpen ? "메뉴 열림 · Esc로 조작 재개" : "WASD 이동 · 마우스 오른쪽 드래그로 시점 · Esc 메뉴";
        }

        private void EnsureHud()
        {
            if (hud != null)
            {
                return;
            }

            hud = new GameObject("Content HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            hud.transform.SetParent(transform, false);
            var canvas = hud.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            font = screen.GetComponentInChildren<Text>(true)?.font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var panel = new GameObject("Toolbar", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            panel.transform.SetParent(hud.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0, 56);
            panel.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.18f, 0.92f);
            var layout = panel.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            notice = CreateLabel("콘텐츠 씬 준비 중", panel.transform);
            notice.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            menuButton = CreateButton("메뉴 (Esc)", panel.transform, () => SetMenuOpen(true));
            CreateModelControlStatus();
            partDetails = new ModelPartDetailsPanel(hud.transform, font, () => modelViewer?.ClearSelection());
            partList = new ModelPartListPanel(hud.transform, font);
            stepPanel = new ModelStepPanel(hud.transform, font, () => actionMenu.CancelPendingSliderChanges());
            actionMenu = new ContentActionMenu(hud.transform, font);
            CreatePauseMenu();
        }

        // 조작 안내와 분리해 현재 소유자를 표시합니다. 레이캐스트를 받지 않아 모델 클릭을 가리지 않습니다.
        private void CreateModelControlStatus()
        {
            modelControlPanel = new GameObject("Model Control Panel", typeof(RectTransform), typeof(Image));
            modelControlPanel.transform.SetParent(hud.transform, false);
            var rect = modelControlPanel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, -68);
            rect.sizeDelta = new Vector2(600, 44);
            var background = modelControlPanel.GetComponent<Image>();
            background.color = new Color32(15, 23, 42, 230);
            background.raycastTarget = false;
            modelControlStatus = CreateLabel("모델 조작 가능", modelControlPanel.transform);
            modelControlStatus.gameObject.name = "Model Control Status";
            modelControlStatus.alignment = TextAnchor.MiddleCenter;
            modelControlStatus.resizeTextForBestFit = true;
            modelControlStatus.resizeTextMinSize = 14;
            modelControlStatus.resizeTextMaxSize = 18;
            modelControlStatus.rectTransform.anchorMin = Vector2.zero;
            modelControlStatus.rectTransform.anchorMax = Vector2.one;
            modelControlStatus.rectTransform.offsetMin = new Vector2(12, 4);
            modelControlStatus.rectTransform.offsetMax = new Vector2(-12, -4);
            modelControlPanel.SetActive(false);
        }

        // 콘텐츠 유형별로 기능을 등록합니다. 영상·시나리오 기능도 구현 시 이 메뉴에 각각 추가할 수 있습니다.
        private void ConfigureContentActions(string contentType)
        {
            actionMenu.ClearActions();
            if (contentType == "model")
            {
                actionMenu.AddAction("부품 목록", () => partList.Toggle(), () => modelViewer != null && modelViewer.Parts.Count > 0);
                actionMenu.AddPlaybackControls(
                    direction => modelViewer?.SetPlayback(playerCamera, direction),
                    direction => modelViewer != null && modelViewer.CanSetPlayback(playerCamera, direction),
                    () => modelViewer != null ? modelViewer.PlaybackStatus : "준비 중");
                actionMenu.AddSharedSlider("분해 보기", "Model Explosion",
                    () => modelViewer != null ? modelViewer.ExplosionAmount : 0,
                    value => modelViewer?.SetExplosion(playerCamera, value),
                    () => modelViewer != null && modelViewer.CanSetExplosion(playerCamera));
                actionMenu.AddTransparencyControls(
                    (enabled, opacity) => modelViewer?.SetSurroundingsTransparency(enabled, opacity),
                    () => modelViewer != null && modelViewer.SelectedPart != null);
                actionMenu.AddAction("회전 초기화",
                    () => modelViewer?.ResetRotation(playerCamera),
                    () => modelViewer != null && modelViewer.CanResetRotation(playerCamera));
            }
        }

        // 화면 전체의 배경이 뒤쪽 UI 클릭을 막고, 중앙 창에서 계속하기·복귀·퇴장을 선택합니다.
        // 멀티플레이 시간은 멈추지 않으며, 메뉴를 연 사용자 자신의 이동과 시점 입력만 차단합니다.
        private void CreatePauseMenu()
        {
            pauseMenu = new GameObject("Content Menu Overlay", typeof(RectTransform), typeof(Image));
            pauseMenu.transform.SetParent(hud.transform, false);
            var overlay = pauseMenu.GetComponent<RectTransform>();
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            pauseMenu.GetComponent<Image>().color = new Color(0, 0, 0, 0.6f);

            var card = new GameObject("Content Menu", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            card.transform.SetParent(pauseMenu.transform, false);
            menuCard = card;
            var rect = card.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(480, 380);
            rect.anchoredPosition = Vector2.zero;
            card.GetComponent<Image>().color = new Color32(30, 41, 59, 255);
            var layout = card.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 20);
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = CreateLabel("콘텐츠 메뉴", card.transform);
            title.fontSize = 24;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
            var hint = CreateLabel("메뉴를 연 동안 내 캐릭터의 조작이 중지됩니다.", card.transform);
            hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
            resumeButton = CreateButton("계속하기 (Esc)", card.transform, () => SetMenuOpen(false));
            CreateButton("설정", card.transform, OpenOptions);
            returnButton = CreateButton("대기로 복귀", card.transform, () => screen.UiTogglePhase());
            leaveButton = CreateButton("방 나가기", card.transform, () => screen.UiLeaveRoom());
            optionsPanel = new ContentOptionsPanel(pauseMenu.transform, font, preferences, ApplyPreferences, SaveOptions, CloseOptions);
            pauseMenu.SetActive(false);
        }

        // 변경 값은 미리보기로 즉시 적용합니다. 영구 저장은 SaveOptions에서만 수행합니다.
        private void ApplyPreferences()
        {
            AudioListener.volume = preferences.Volume;
            if (playerCamera != null)
            {
                playerCamera.fieldOfView = preferences.FieldOfView;
            }
        }

        private void SetMenuOpen(bool value)
        {
            menuOpen = value;
            if (value)
            {
                actionMenu?.Close();
                partList?.Close();
            }
            if (!value)
            {
                CloseOptions();
            }
        }

        private void OpenOptions()
        {
            preferences.BeginEdit();
            optionsPanel.Refresh();
            optionsOpen = true;
        }

        private void SaveOptions()
        {
            preferences.Save();
            optionsOpen = false;
        }

        // Esc·취소·메뉴 종료·퇴장 모두 같은 취소 경로를 사용해 감도·FOV·음량을 복구합니다.
        private void CloseOptions()
        {
            if (!optionsOpen)
            {
                return;
            }

            preferences.CancelEdit();
            ApplyPreferences();
            optionsPanel.Refresh();
            optionsOpen = false;
        }

        private Text CreateLabel(string value, Transform parent)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.font = font;
            label.fontSize = 16;
            label.color = Color.white;
            label.supportRichText = false;
            label.alignment = TextAnchor.MiddleLeft;
            label.text = value;
            label.raycastTarget = false;
            return label;
        }

        private Button CreateButton(string value, Transform parent, UnityEngine.Events.UnityAction action)
        {
            var button = new GameObject(value, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement)).GetComponent<Button>();
            button.transform.SetParent(parent, false);
            button.targetGraphic = button.GetComponent<Image>();
            button.GetComponent<Image>().color = new Color32(51, 65, 85, 255);
            var size = button.GetComponent<LayoutElement>();
            size.preferredWidth = 140;
            size.minHeight = size.preferredHeight = 42;
            var label = CreateLabel(value, button.transform);
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            button.onClick.AddListener(action);
            return button;
        }

        private static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void EndFirstPerson()
        {
            CloseOptions();
            ReleaseCursor();
            if (playerCamera != null)
            {
                playerCamera.gameObject.SetActive(false);
                Destroy(playerCamera.gameObject);
                playerCamera = null;
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
            actionMenu?.Close();
            partList?.Close();
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
            actionMenu = null;
            modelViewer = null;
            inspectionZoom = 1f;
            EndFirstPerson();
        }
    }
}
