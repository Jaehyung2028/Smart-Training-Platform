using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SmartTraining
{
    internal struct ContentHudState
    {
        public bool Active, Requested, SceneReady, Busy, CanInteract, IsMaster, CanReturn, CanLeave;
        public string Error, ContentMessage;
        public ModelViewer Model;
        public VideoContentPlayer Video;
        public ScenarioContent Scenario;
        public Camera Camera;
        public TrainingProgress Progress;
        public string RoomId, LocalUserId, ChatStatus;
        public IReadOnlyList<LobbyChatMessage> ChatMessages;
        public bool CanSendChat;
        public ContentParticipantState[] Participants;
    }


    internal sealed class ContentHudActions
    {
        public Action SettingsChanged;
        public Action ReturnToWaiting;
        public Action LeaveRoom;
        public Action<string, Action<bool>> SendChat;
    }


    // 콘텐츠 화면·메뉴·설정 편집을 담당합니다. API 화면이나 씬 수명주기 클래스는 참조하지 않습니다.
    internal sealed class ContentHudController : IDisposable
    {
        private readonly ClientPreferences preferences;
        private readonly ContentHudActions actions;
        private readonly Font font;
        private readonly InputAction menuAction;
        private ContentHudState state = new()
        {
            Error = "",
            ContentMessage = ""
        };
        private GameObject pauseMenu, menuCard, modelControlPanel;
        private ClientSettingsPanel optionsPanel;
        private ContentActionMenu actionMenu;
        private ModelPartDetailsPanel partDetails;
        private ModelPartListPanel partList;
        private VideoControlPanel videoPanel;
        private ScenarioControlPanel scenarioPanel;
        private ScenarioResultPanel scenarioResults;
        private ScenarioToolMenu scenarioTools;
        private ScenarioRoleMenu scenarioRoles;
        private ContentChatPanel chatPanel;
        private Button chatButton, participantsButton;
        private ContentParticipantPanel participantsPanel;
        private Text notice, modelControlStatus, learningStatus;
        private RectTransform learningPanel;
        private Button resumeButton, menuButton, returnButton, leaveButton;
        private bool menuOpen, optionsOpen;

        public GameObject Root { get; }

        public bool MenuOpen => menuOpen;

        private bool HasScenarioResults => state.Requested && state.SceneReady && state.Scenario?.IsComplete == true;

        public bool BlocksGameplayInput => menuOpen || state.Error.Length > 0 || HasScenarioResults || chatPanel?.IsEditing == true;


        public ContentHudController(
            UiPrefabReferences view,
            Font font,
            ClientPreferences preferences,
            ContentHudActions actions
        )
        {
            this.font = font;
            this.preferences = preferences;
            this.actions = actions;
            pauseMenu = view.Get<GameObject>("pauseMenu");
            menuCard = view.Get<GameObject>("menuCard");
            modelControlPanel = view.Get<GameObject>("modelControlPanel");
            chatButton = view.Get<Button>("chatButton");
            participantsButton = view.Get<Button>("participantsButton");
            notice = view.Get<Text>("notice");
            modelControlStatus = view.Get<Text>("modelControlStatus");
            learningStatus = view.Get<Text>("learningStatus");
            learningPanel = view.Get<RectTransform>("learningPanel");
            resumeButton = view.Get<Button>("resumeButton");
            menuButton = view.Get<Button>("menuButton");
            returnButton = view.Get<Button>("returnButton");
            leaveButton = view.Get<Button>("leaveButton");
            Root = view.Get<GameObject>("Root");
            participantsButton.onClick.AddListener(() =>
            {
                chatPanel.Close();
                participantsPanel.Toggle();
            });
            chatButton.onClick.AddListener(() =>
            {
                participantsPanel.Close();
                chatPanel.Toggle();
            });
            menuButton.onClick.AddListener(() => SetMenuOpen(true));
            resumeButton.onClick.AddListener(() => SetMenuOpen(false));
            view.Get<Button>("설정:Button").onClick.AddListener(OpenOptions);
            returnButton.onClick.AddListener(() => actions.ReturnToWaiting?.Invoke());
            leaveButton.onClick.AddListener(() => actions.LeaveRoom?.Invoke());
            partDetails = new ModelPartDetailsPanel(Root.transform, font, () => state.Model?.ClearSelection());
            partList = new ModelPartListPanel(Root.transform, font);
            actionMenu = new ContentActionMenu(Root.transform, font);
            scenarioPanel = new ScenarioControlPanel(Root.transform, font);
            scenarioResults = new ScenarioResultPanel(Root.transform, font, () => actions.ReturnToWaiting?.Invoke());
            chatPanel = new ContentChatPanel(
                Root.transform,
                font,
                (text, completed) => actions.SendChat?.Invoke(text, completed)
            );
            participantsPanel = new ContentParticipantPanel(Root.transform, font);
            optionsPanel = new ClientSettingsPanel(
                pauseMenu.transform,
                font,
                preferences,
                ApplyPreferences,
                SaveOptions,
                CloseOptions
            );
            pauseMenu.transform.SetAsLastSibling();
            menuAction = new InputAction("Content Menu", InputActionType.Button, "<Keyboard>/escape");
            menuAction.performed += ToggleMenu;
            EnableInput();
        }


        public void EnableInput()
        {
            menuAction.Enable();
        }


        public void DisableInput()
        {
            menuAction.Disable();
            state.Requested = false;
            CloseOptions();
        }


        public void ClearContent()
        {
            participantsPanel.Close();
            chatPanel.Close();
            videoPanel?.Dispose();
            videoPanel = null;
            actionMenu.ClearActions();
            scenarioTools = null;
            scenarioRoles = null;
            partList.Clear();
            state.Model = null;
            state.Video = null;
            state.Scenario = null;
        }


        public void EndSession()
        {
            CloseOptions();
            participantsPanel.Close();
            chatPanel.Close();
            state.Requested = false;
            state.Active = false;
            if (Root != null)
            {
                actionMenu.Close();
                partList.Close();
                Root.SetActive(false);
            }
        }


        private void ApplyPreferences()
        {
            preferences.ApplyAudioVolume();
            actions.SettingsChanged?.Invoke();
        }


        public void Dispose()
        {
            CloseOptions();
            menuAction.Dispose();
            chatPanel.Dispose();
            if (Root != null)
            {
                Root.SetActive(false);
                // HUD 오브젝트는 콘텐츠 씬이 소유하므로 씬 언로드에서 함께 정리합니다.
            }
        }


        public void Refresh(ContentHudState state)
        {
            state.Error ??= "";
            state.ContentMessage ??= "";
            this.state = state;
            Root.SetActive(state.Active);
            notice.text = GetNotice();
            bool showChat = state.Requested && state.SceneReady && !menuOpen && state.Error.Length == 0;
            chatButton.interactable = showChat;
            participantsButton.interactable = showChat;
            participantsPanel.Refresh(state.Participants, showChat);
            participantsButton.GetComponentInChildren<Text>().text = "참가자 (" + (state.Participants?.Length ?? 0) + ")";
            chatPanel.Refresh(
                state.RoomId,
                state.LocalUserId,
                state.ChatMessages,
                state.ChatStatus,
                showChat,
                state.CanSendChat
            );
            chatButton.GetComponentInChildren<Text>().text = chatPanel.IsOpen ? "채팅 닫기" : "방 채팅";
            // 기본 위치는 HUD 프리팹에서 편집합니다.
            learningStatus.text = state.Progress == null ? "학습 준비 중" : state.Progress.Label;
            if (state.Model != null)
            {
                learningStatus.text += "\n부품 확인 " + state.Model.ObservedPartCount + " / " + state.Model.RequiredPartCount;
            }

            learningStatus.color = state.Progress?.IsComplete == true ? new Color32(74, 222, 128, 255) : Color.white;
            learningPanel.gameObject.SetActive(state.Requested && state.SceneReady && !menuOpen);
            bool showModelStatus = state.Requested && state.SceneReady && state.Model != null && !menuOpen && state.Error.Length == 0;
            modelControlPanel.SetActive(showModelStatus);
            videoPanel?.Refresh(
                state.Video,
                state.Requested && state.SceneReady && !menuOpen && state.Error.Length == 0,
                state.CanInteract
            );
            scenarioPanel.Refresh(
                state.Scenario,
                state.Requested && state.SceneReady && !menuOpen && !HasScenarioResults && state.Error.Length == 0
            );
            scenarioRoles?.Refresh(
                state.Scenario,
                state.Requested && state.SceneReady && !menuOpen && !HasScenarioResults && state.Error.Length == 0,
                state.CanInteract
            );
            scenarioTools?.Refresh(
                state.Scenario,
                state.Requested && state.SceneReady && !menuOpen && !HasScenarioResults && state.Error.Length == 0,
                state.CanInteract
            );
            scenarioResults.Refresh(
                state.Scenario,
                HasScenarioResults && !menuOpen && state.Error.Length == 0,
                state.CanInteract && !state.Busy,
                state.CanReturn
            );
            partDetails.Refresh(state.Model, showModelStatus);
            partList.Refresh(state.Model, showModelStatus, state.CanInteract);
            if (showModelStatus)
            {
                modelControlStatus.text = state.Model.GetControlStatus();
                modelControlStatus.color = state.Model.IsLocalManipulator ? new Color32(74, 222, 128, 255) : state.Model.IsBeingManipulated ? new Color32(251, 191, 36, 255) : Color.white;
            }

            pauseMenu.SetActive(state.Requested && (menuOpen || state.Error.Length > 0));
            menuCard.SetActive(!optionsOpen);
            optionsPanel.Root.SetActive(optionsOpen);
            menuButton.interactable = state.Requested;
            actionMenu.Refresh(
                state.Requested && state.SceneReady && !HasScenarioResults,
                !menuOpen && state.Error.Length == 0 && state.CanInteract
            );
            resumeButton.interactable = state.SceneReady && !state.Busy;
            returnButton.gameObject.SetActive(state.IsMaster);
            returnButton.interactable = state.CanReturn;
            leaveButton.interactable = state.CanLeave;
        }


        private void ToggleMenu(InputAction.CallbackContext context)
        {
            HandleBack();
        }


        // Esc와 다른 UI 내비게이션이 동일한 닫기 우선순위와 설정 취소 경로를 사용합니다.
        internal void HandleBack()
        {
            if (state.Requested && Root != null)
            {
                if (chatPanel.IsOpen)
                {
                    chatPanel.Close();
                }
                else if (participantsPanel.IsOpen)
                {
                    participantsPanel.Close();
                }
                else if (optionsOpen)
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


        private string GetNotice()
        {
            if (state.Error.Length > 0)
            {
                return state.Error;
            }

            if (!state.SceneReady)
            {
                return state.ContentMessage.Length > 0 ? state.ContentMessage : "콘텐츠 씬을 준비하고 있습니다…";
            }

            if (!menuOpen && state.ContentMessage.Length > 0)
            {
                return state.ContentMessage;
            }

            if (!menuOpen && state.Model != null)
            {
                return "WASD 이동 · 우클릭 드래그 시점 · 부품 클릭 설명 · 좌클릭 드래그 회전 · 휠 버튼 누르는 동안 응시 · Esc 메뉴";
            }

            if (!menuOpen && state.Scenario != null)
            {
                return "WASD 이동 · 우클릭 드래그 시점 · 메뉴에서 내 공구 선택 · 대상 좌클릭 실습 · 휠 버튼 응시 · Esc 메뉴";
            }

            return menuOpen ? "메뉴 열림 · Esc로 조작 재개" : "WASD 이동 · 우클릭 드래그 시점 · 휠 버튼 누르는 동안 응시 · Esc 메뉴";
        }


        // 조작 안내와 분리해 현재 소유자를 표시합니다. 레이캐스트를 받지 않아 모델 클릭을 가리지 않습니다.
        // 콘텐츠 유형별로 기능을 등록합니다. 영상·시나리오 기능도 구현 시 이 메뉴에 각각 추가할 수 있습니다.
        internal void ConfigureContentActions(string contentType)
        {
            videoPanel?.Dispose();
            videoPanel = null;
            actionMenu.ClearActions();
            scenarioTools = null;
            scenarioRoles = null;
            if (contentType == "video")
            {
                videoPanel = new VideoControlPanel(actionMenu.AddSection("Video Menu"), Root.transform, font);
            }
            else if (contentType == "scenario")
            {
                scenarioRoles = new ScenarioRoleMenu(actionMenu.AddSection("Scenario Role Menu"), font);
                scenarioTools = new ScenarioToolMenu(actionMenu.AddSection("Scenario Tool Menu"), font);
                actionMenu.AddAction(
                    "실습 처음부터 (방장)",
                    () => state.Scenario?.ResetPractice(),
                    () => state.Scenario != null && state.Scenario.CanReset
                );
            }
            else if (contentType == "model")
            {
                actionMenu.AddAction(
                    "부품 목록",
                    () => partList.Toggle(),
                    () => state.Model != null && state.Model.Parts.Count > 0
                );
                actionMenu.AddPlaybackControls(
                    direction => state.Model?.SetPlayback(state.Camera, direction),
                    direction => state.Model != null && state.Model.CanSetPlayback(state.Camera, direction),
                    () => state.Model != null ? state.Model.PlaybackStatus : "준비 중"
                );
                actionMenu.AddSharedSlider(
                    "분해 보기",
                    "Model Explosion",
                    () => state.Model != null ? state.Model.ExplosionAmount : 0,
                    value => state.Model?.SetExplosion(state.Camera, value),
                    () => state.Model != null && state.Model.CanSetExplosion(state.Camera)
                );
                actionMenu.AddTransparencyControls(
                    (enabled, opacity) => state.Model?.SetSurroundingsTransparency(enabled, opacity),
                    () => state.Model != null && state.Model.SelectedPart != null
                );
                actionMenu.AddAction(
                    "회전 초기화",
                    () => state.Model?.ResetRotation(state.Camera),
                    () => state.Model != null && state.Model.CanResetRotation(state.Camera)
                );
            }
        }


        // 화면 전체의 배경이 뒤쪽 UI 클릭을 막고, 중앙 창에서 계속하기·복귀·퇴장을 선택합니다.
        // 멀티플레이 시간은 멈추지 않으며, 메뉴를 연 사용자 자신의 이동과 시점 입력만 차단합니다.
        internal void SetMenuOpen(bool value)
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
        internal void CloseOptions()
        {
            if (!optionsOpen)
            {
                return;
            }

            preferences.CancelEdit();
            ApplyPreferences();
            if (optionsPanel.Root != null)
            {
                optionsPanel.Refresh();
            }

            optionsOpen = false;
        }
    }
}
