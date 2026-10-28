using System;
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
    }


    internal sealed class ContentHudActions
    {
        public Action SettingsChanged;
        public Action ReturnToWaiting;
        public Action LeaveRoom;
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
        private ModelStepPanel stepPanel;
        private ModelPartListPanel partList;
        private VideoControlPanel videoPanel;
        private ScenarioControlPanel scenarioPanel;
        private ScenarioToolMenu scenarioTools;
        private Text notice, modelControlStatus, learningStatus;
        private RectTransform learningPanel;
        private Button resumeButton, menuButton, returnButton, leaveButton;
        private bool menuOpen, optionsOpen;

        public GameObject Root { get; }

        public bool MenuOpen => menuOpen;

        public bool BlocksGameplayInput => menuOpen || state.Error.Length > 0;


        public ContentHudController(
            Transform parent,
            Font font,
            ClientPreferences preferences,
            ContentHudActions actions
        )
        {
            this.font = font;
            this.preferences = preferences;
            this.actions = actions;
            Root = new GameObject(
                "Content HUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );
            Root.transform.SetParent(parent, false);
            CreateHud();
            UguiVisualStyle.PolishDark(Root.transform);
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
            actionMenu.ClearActions();
            scenarioTools = null;
            partList.Clear();
            state.Model = null;
            state.Video = null;
            state.Scenario = null;
        }


        public void EndSession()
        {
            CloseOptions();
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
            if (Root != null)
            {
                Root.SetActive(false);
                UnityEngine.Object.Destroy(Root);
            }
        }


        public void Refresh(ContentHudState state)
        {
            state.Error ??= "";
            state.ContentMessage ??= "";
            this.state = state;
            Root.SetActive(state.Active);
            notice.text = GetNotice();
            learningPanel.anchoredPosition = new Vector2(24, state.Video != null ? 306 : 24);
            learningStatus.text = state.Progress == null ? "학습 준비 중" : state.Progress.Label;
            if (state.Model != null)
            {
                learningStatus.text += "\n부품 확인 " + state.Model.ObservedPartCount + " / " + state.Model.RequiredPartCount;
            }
            learningStatus.color = state.Progress?.IsComplete == true ? new Color32(74, 222, 128, 255) : Color.white;
            learningPanel.gameObject.SetActive(state.Requested && state.SceneReady && !menuOpen);
            bool showModelStatus = state.Requested && state.SceneReady && state.Model != null && !menuOpen && state.Error.Length == 0;
            modelControlPanel.SetActive(showModelStatus);
            videoPanel.Refresh(
                state.Video,
                state.Requested && state.SceneReady && !menuOpen && state.Error.Length == 0,
                state.CanInteract
            );
            scenarioPanel.Refresh(state.Scenario,
                state.Requested && state.SceneReady && !menuOpen && state.Error.Length == 0);
            scenarioTools?.Refresh(state.Scenario,
                state.Requested && state.SceneReady && !menuOpen && state.Error.Length == 0, state.CanInteract);
            partDetails.Refresh(state.Model, showModelStatus);
            stepPanel.Refresh(state.Model, state.Camera, showModelStatus, state.CanInteract);
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
                state.Requested && state.SceneReady,
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


        private void CreateHud()
        {
            var canvas = Root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var panel = new GameObject("Toolbar", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            panel.transform.SetParent(Root.transform, false);
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
            learningPanel = new GameObject("Learning Progress Card", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            learningPanel.SetParent(Root.transform, false);
            learningPanel.anchorMin = learningPanel.anchorMax = learningPanel.pivot = Vector2.zero;
            learningPanel.anchoredPosition = new Vector2(24, 24);
            learningPanel.sizeDelta = new Vector2(280, 65);
            learningPanel.GetComponent<Image>().color = new Color32(15, 23, 42, 242);
            learningPanel.GetComponent<Image>().raycastTarget = false;
            learningStatus = CreateLabel("학습 준비 중", learningPanel);
            learningStatus.gameObject.name = "Learning Progress";
            learningStatus.raycastTarget = false;
            learningStatus.fontSize = 16;
            learningStatus.alignment = TextAnchor.MiddleLeft;
            var learningRect = learningStatus.rectTransform;
            learningRect.anchorMin = Vector2.zero;
            learningRect.anchorMax = Vector2.one;
            learningRect.offsetMin = new Vector2(14, 8);
            learningRect.offsetMax = new Vector2(-14, -8);
            CreateModelControlStatus();
            partDetails = new ModelPartDetailsPanel(Root.transform, font, () => state.Model?.ClearSelection());
            partList = new ModelPartListPanel(Root.transform, font);
            stepPanel = new ModelStepPanel(Root.transform, font, () => actionMenu.CancelPendingSliderChanges());
            actionMenu = new ContentActionMenu(Root.transform, font);
            videoPanel = new VideoControlPanel(Root.transform, font);
            scenarioPanel = new ScenarioControlPanel(Root.transform, font);
            CreatePauseMenu();
        }


        // 조작 안내와 분리해 현재 소유자를 표시합니다. 레이캐스트를 받지 않아 모델 클릭을 가리지 않습니다.
        private void CreateModelControlStatus()
        {
            modelControlPanel = new GameObject("Model Control Panel", typeof(RectTransform), typeof(Image));
            modelControlPanel.transform.SetParent(Root.transform, false);
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
        internal void ConfigureContentActions(string contentType)
        {
            actionMenu.ClearActions();
            scenarioTools = null;
            if (contentType == "video")
            {
                actionMenu.AddAction(
                    "영상 재생·일시정지",
                    () => state.Video?.TogglePlayback(),
                    () => state.Video != null && state.Video.CanControl
                );
                actionMenu.AddAction(
                    "영상 처음으로",
                    () => state.Video?.Seek(0),
                    () => state.Video != null && state.Video.CanControl
                );
            }
            else if (contentType == "scenario")
            {
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
        private void CreatePauseMenu()
        {
            pauseMenu = new GameObject("Content Menu Overlay", typeof(RectTransform), typeof(Image));
            pauseMenu.transform.SetParent(Root.transform, false);
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
            returnButton = CreateButton("대기로 복귀", card.transform, () => actions.ReturnToWaiting?.Invoke());
            leaveButton = CreateButton("방 나가기", card.transform, () => actions.LeaveRoom?.Invoke());
            optionsPanel = new ClientSettingsPanel(
                pauseMenu.transform,
                font,
                preferences,
                ApplyPreferences,
                SaveOptions,
                CloseOptions
            );
            pauseMenu.SetActive(false);
        }


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
            var button = new GameObject(
                value,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement)
            ).GetComponent<Button>();
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
    }
}
