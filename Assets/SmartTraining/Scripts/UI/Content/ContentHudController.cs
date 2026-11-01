using System.Collections.Generic;
using System;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine;

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

    // 콘텐츠 HUD와 메뉴를 갱신하고 옵션의 미리보기·저장·취소를 처리합니다.
    internal sealed class ContentHudController : IDisposable
    {
        private readonly ClientPreferences preferences;
        private readonly ContentHudActions actions;
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
        private readonly Text chatLabel, participantsLabel;
        private ContentParticipantPanel participantsPanel;
        private Text notice, modelControlStatus, learningStatus;
        private RectTransform learningPanel;
        private Button resumeButton, menuButton, returnButton, leaveButton;
        private bool menuOpen, optionsOpen;

        public GameObject Root { get; }

        public bool MenuOpen => menuOpen;

        private bool HasScenarioResults => state.Requested && state.SceneReady && state.Scenario?.IsComplete == true;

        public bool BlocksGameplayInput => menuOpen || state.Error.Length > 0 || HasScenarioResults || chatPanel?.IsEditing == true;

        public ContentHudController(UiPrefabReferences view, ClientPreferences preferences, ContentHudActions actions)
        {
            this.preferences = preferences;
            this.actions = actions;
            pauseMenu = view.Get<GameObject>("pauseMenu");
            menuCard = view.Get<GameObject>("menuCard");
            modelControlPanel = view.Get<GameObject>("modelControlPanel");
            chatButton = view.Get<Button>("chatButton");
            participantsButton = view.Get<Button>("participantsButton");
            chatLabel = chatButton.GetComponentInChildren<Text>(true);
            participantsLabel = participantsButton.GetComponentInChildren<Text>(true);
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
            partDetails = new ModelPartDetailsPanel(Root.transform, () => state.Model?.ClearSelection());
            partList = new ModelPartListPanel(Root.transform);
            actionMenu = new ContentActionMenu(Root.transform);
            scenarioPanel = new ScenarioControlPanel(Root.transform);
            scenarioResults = new ScenarioResultPanel(Root.transform, () => actions.ReturnToWaiting?.Invoke());
            chatPanel = new ContentChatPanel(Root.transform, (text, completed) => actions.SendChat?.Invoke(text, completed));
            participantsPanel = new ContentParticipantPanel(Root.transform);
            optionsPanel = new ClientSettingsPanel(pauseMenu.transform, preferences, ApplyPreferences, SaveOptions, CloseOptions);
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
            ClearContentActions();
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
                // HUD 오브젝트는 씬 언로드에서 해제합니다.
            }
        }

        public void Refresh(ContentHudState state)
        {
            state.Error ??= "";
            state.ContentMessage ??= "";
            this.state = state;
            Root.SetActive(state.Active);
            notice.text = GetNotice();
            bool visible = state.Requested && state.SceneReady && !menuOpen && state.Error.Length == 0;
            RefreshSocialPanels(visible);
            RefreshLearningProgress();
            RefreshContentPanels(visible);
            RefreshMenu();
        }

        private void RefreshSocialPanels(bool visible)
        {
            chatButton.interactable = visible;
            participantsButton.interactable = visible;
            participantsPanel.Refresh(state.Participants, visible);
            participantsLabel.text = "참가자 (" + (state.Participants?.Length ?? 0) + ")";
            chatPanel.Refresh(state.RoomId, state.LocalUserId, state.ChatMessages, state.ChatStatus, visible, state.CanSendChat);
            chatLabel.text = chatPanel.IsOpen ? "채팅 닫기" : "방 채팅";
        }

        private void RefreshLearningProgress()
        {
            learningStatus.text = state.Progress == null ? "학습 준비 중" : state.Progress.Label;
            if (state.Model != null)
            {
                learningStatus.text += "\n부품 확인 " + state.Model.ObservedPartCount + " / " + state.Model.RequiredPartCount;
            }

            learningStatus.color = state.Progress?.IsComplete == true ? new Color32(74, 222, 128, 255) : Color.white;
            learningPanel.gameObject.SetActive(state.Requested && state.SceneReady && !menuOpen);
        }

        private void RefreshContentPanels(bool visible)
        {
            bool scenarioVisible = visible && !HasScenarioResults;
            bool showModelStatus = visible && state.Model != null;
            modelControlPanel.SetActive(showModelStatus);
            videoPanel?.Refresh(state.Video, visible, state.CanInteract);
            scenarioPanel.Refresh(state.Scenario, scenarioVisible);
            scenarioRoles?.Refresh(state.Scenario, scenarioVisible, state.CanInteract);
            scenarioTools?.Refresh(state.Scenario, scenarioVisible, state.CanInteract);
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
                modelControlStatus.color = GetModelControlColor(state.Model);
            }
        }

        private static Color GetModelControlColor(ModelViewer model)
        {
            if (model.IsLocalManipulator)
            {
                return new Color32(74, 222, 128, 255);
            }

            if (model.IsBeingManipulated)
            {
                return new Color32(251, 191, 36, 255);
            }

            return Color.white;
        }

        private void RefreshMenu()
        {
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

        private void ClearContentActions()
        {
            videoPanel?.Dispose();
            videoPanel = null;
            actionMenu.ClearActions();
            scenarioTools = null;
            scenarioRoles = null;
        }

        // 콘텐츠를 바꿀 때 이전 메뉴와 이벤트 참조를 정리한 뒤 새 기능을 연결합니다.
        internal void ConfigureContentActions(string contentType)
        {
            ClearContentActions();
            if (contentType == "video")
            {
                videoPanel = new VideoControlPanel(actionMenu.AddSection("Video Menu"), Root.transform);
            }
            else if (contentType == "scenario")
            {
                scenarioRoles = new ScenarioRoleMenu(actionMenu.AddSection("Scenario Role Menu"));
                scenarioTools = new ScenarioToolMenu(actionMenu.AddSection("Scenario Tool Menu"));
                actionMenu.AddAction(
                    "실습 처음부터 (방장)",
                    () => state.Scenario?.ResetPractice(),
                    () => state.Scenario != null && state.Scenario.CanReset
                );
            }
            else if (contentType == "model")
            {
                actionMenu.AddAction("부품 목록", () => partList.Toggle(), () => state.Model != null && state.Model.Parts.Count > 0);
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
