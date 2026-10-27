using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster))]
    public sealed class LobbyUguiView : MonoBehaviour
    {
        [Header("Layout")]
        public Canvas canvas;

        public GameObject loginPanel, lobbyPanel, progressPanel, failurePanel;

        public RectTransform lobbyContent;

        public Button lobbyRowTemplate;

        [Header("Text and inputs")]
        public Font fontOverride;

        public InputField addressInput, nicknameInput;

        // 드롭다운은 실행 중 생성합니다. 프리팹에서 미리 연결해야 하는 필수 필드가 아닙니다.
        private RectTransform courseOptionsContent;

        private readonly Dictionary<string, Button> courseButtons = new();

        private string courseOptionsKey = "";

        private Button courseSelectorButton;

        private Text courseSelectionText;

        private GameObject coursePopup;

        private RectTransform courseViewport;

        public Text connectionText, statusText, addressNotice, userText, selectedDetail, progressText, failureText, emptyText;

        [Header("Actions")]
        public Button healthButton, loginButton, saveButton, resetButton, refreshButton, logoutButton, enterButton;

        public Button cancelButton, retryButton, closeFailureButton, reconnectButton;

        private ApiLobbyScreen screen;

        private bool wired;

        private string rowsKey = "";

        private string displayedFailure = "";

        private string displayedDetailLobby = "";

        private readonly List<Button> rows = new();

        private Font runtimeFont;

        private CanvasGroup interaction;

        private GraphicRaycaster raycaster;

        public void Bind(ApiLobbyScreen controller)
        {
            screen = controller;
            EnsureComponents();
            if (wired)
            {
                return;
            }

            wired = true;
            UguiViewUtility.EnsureEventSystem(transform);
            EnsureCourseSelector();
            runtimeFont = UguiViewUtility.ApplyFont(gameObject, fontOverride);
            // 입장 실패 후 인증 세션이 남은 경우에도 이 화면에서 로그아웃할 수 있도록 합니다.
            logoutButton.transform.SetParent(loginButton.transform.parent, false);
            addressInput.onValueChanged.AddListener(value => screen.UiAddress = value);
            nicknameInput.onValueChanged.AddListener(value => screen.UiNickname = value);
            healthButton.onClick.AddListener(() => screen.UiHealth());
            loginButton.onClick.AddListener(() => screen.UiLogin());
            saveButton.onClick.AddListener(() => screen.UiSaveAddress());
            resetButton.onClick.AddListener(() => screen.UiResetAddress());
            refreshButton.onClick.AddListener(() => screen.UiRefresh());
            logoutButton.onClick.AddListener(() => screen.UiLogout());
            enterButton.onClick.AddListener(() => screen.UiEnter());
            cancelButton.onClick.AddListener(() => screen.UiCancel());
            retryButton.onClick.AddListener(() => screen.UiRetry());
            closeFailureButton.onClick.AddListener(() => screen.UiCloseFailure());
            reconnectButton.onClick.AddListener(() => screen.UiReconnectNotifications());
            nicknameInput.onEndEdit.AddListener(_ =>
            {
                if (UnityEngine.InputSystem.Keyboard.current?.enterKey.wasPressedThisFrame == true)
                {
                    screen.UiLogin();
                }
            });
            Refresh();
        }

        private void EnsureComponents() => UguiViewUtility.EnsureComponents(this, ref canvas, ref interaction, ref raycaster);

        private void LateUpdate() => Refresh();

        internal void RefreshState()
        {
            if (wired)
            {
                Refresh();
            }
        }

        internal void ResetTransientState()
        {
            CloseCourseDropdown();
            UguiViewUtility.ClearSelection(transform);
            progressPanel.SetActive(false);
            failurePanel.SetActive(false);
            displayedFailure = "";
            displayedDetailLobby = "";
            progressText.text = failureText.text = "";
            foreach (var row in rows)
            {
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }

            rows.Clear();
            rowsKey = "";
            emptyText.gameObject.SetActive(true);
        }

        private void Refresh()
        {
            if (screen == null)
            {
                return;
            }

            EnsureComponents();
            bool visible = screen.UseUgui;
            UguiViewUtility.SetVisible(this, canvas, interaction, raycaster, visible);
            if (!visible)
            {
                CloseCourseDropdown();
                loginPanel.SetActive(false);
                lobbyPanel.SetActive(false);
                progressPanel.SetActive(false);
                failurePanel.SetActive(false);
                progressText.text = failureText.text = "";
                return;
            }

            bool processing = screen.UiProcessing, loggedIn = screen.UiLoggedIn;
            bool joining = !string.IsNullOrEmpty(screen.UiProgress);
            loginPanel.SetActive(!joining);
            lobbyPanel.SetActive(false);
            progressPanel.SetActive(joining);
            failurePanel.SetActive(loggedIn && !joining && !string.IsNullOrEmpty(screen.UiFailure));
            addressInput.SetTextWithoutNotify(screen.UiAddress);
            nicknameInput.SetTextWithoutNotify(screen.UiNickname);
            addressInput.interactable = nicknameInput.interactable = !processing && !loggedIn;
            healthButton.interactable = !processing;
            saveButton.interactable = resetButton.interactable = !processing && !loggedIn;
            loginButton.interactable = screen.UiCanCourseLogin;
            loginButton.GetComponentInChildren<Text>().text = loggedIn ? "과정 입장" : "로그인 후 입장";
            logoutButton.gameObject.SetActive(loggedIn);
            UpdateCourses();
            refreshButton.interactable = logoutButton.interactable = reconnectButton.interactable = !processing;
            enterButton.interactable = screen.UiCanEnter;
            cancelButton.interactable = screen.UiCanCancel;
            retryButton.interactable = screen.UiCanRetry;
            closeFailureButton.interactable = !processing;
            connectionText.text = screen.UiConnection;
            statusText.text = screen.UiStatus;
            addressNotice.text = screen.UiAddressNotice;
            userText.text = "콘텐츠 로비  /  " + screen.UiUser;
            selectedDetail.text = screen.UiSelectedDetail();
            if (displayedDetailLobby != screen.UiSelectedLobby)
            {
                Canvas.ForceUpdateCanvases();
                var details = selectedDetail.GetComponentInParent<ScrollRect>(true);
                if (details != null)
                {
                    details.verticalNormalizedPosition = 1;
                }

                displayedDetailLobby = screen.UiSelectedLobby;
            }

            progressText.text = screen.UiProgressTitle + "\n" + screen.UiProgress;
            failureText.text = "로비 입장 안내\n" + screen.UiFailure;
            UpdateRows();
            if (failurePanel.activeInHierarchy && displayedFailure != screen.UiFailure)
            {
                Canvas.ForceUpdateCanvases();
                lobbyContent.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 1;
            }

            displayedFailure = failurePanel.activeInHierarchy ? screen.UiFailure : "";
            foreach (var row in rows)
            {
                row.interactable = !processing;
            }
        }

        // 기존 프리팹을 덮어쓰지 않고 닉네임 아래에 교육과정 드롭다운을 추가합니다.
        private void EnsureCourseSelector()
        {
            if (courseOptionsContent != null)
            {
                return;
            }
            var group = new GameObject("Course Selection", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            group.transform.SetParent(nicknameInput.transform.parent, false);
            group.transform.SetSiblingIndex(loginButton.transform.parent.GetSiblingIndex());
            var layout = group.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            group.GetComponent<LayoutElement>().minHeight = 70;
            group.GetComponent<LayoutElement>().preferredHeight = 70;
            var label = MakeCourseLabel("교육과정 선택", group.transform);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 22;

            courseSelectorButton = new GameObject("Course Dropdown", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement)).GetComponent<Button>();
            courseSelectorButton.transform.SetParent(group.transform, false);
            courseSelectorButton.targetGraphic = courseSelectorButton.GetComponent<Image>();
            courseSelectorButton.GetComponent<LayoutElement>().preferredHeight = 44;
            courseSelectionText = MakeCourseLabel("교육과정을 선택해주세요  ▾", courseSelectorButton.transform);
            courseSelectionText.rectTransform.anchorMin = Vector2.zero;
            courseSelectionText.rectTransform.anchorMax = Vector2.one;
            courseSelectionText.rectTransform.offsetMin = new Vector2(10, 2);
            courseSelectionText.rectTransform.offsetMax = new Vector2(-10, -2);
            courseSelectorButton.onClick.AddListener(ToggleCourseDropdown);

            // 전체 화면의 투명 버튼은 목록 밖 클릭을 받아 닫습니다. 별도 Canvas로 다른 입력창 위에 표시합니다.
            coursePopup = new GameObject("Course Dropdown Popup", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image), typeof(Button));
            coursePopup.transform.SetParent(transform, false);
            var popupRect = coursePopup.GetComponent<RectTransform>();
            popupRect.anchorMin = Vector2.zero;
            popupRect.anchorMax = Vector2.one;
            popupRect.offsetMin = popupRect.offsetMax = Vector2.zero;
            var popupCanvas = coursePopup.GetComponent<Canvas>();
            popupCanvas.overrideSorting = true;
            popupCanvas.sortingOrder = 100;
            coursePopup.GetComponent<Image>().color = Color.clear;
            coursePopup.GetComponent<Button>().transition = Selectable.Transition.None;
            coursePopup.GetComponent<Button>().onClick.AddListener(CloseCourseDropdown);

            var viewport = new GameObject("Course List", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect), typeof(LayoutElement));
            viewport.transform.SetParent(coursePopup.transform, false);
            courseViewport = viewport.GetComponent<RectTransform>();
            courseViewport.anchorMin = courseViewport.anchorMax = new Vector2(0, 1);
            courseViewport.pivot = new Vector2(0, 1);
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<LayoutElement>().minHeight = 128;
            viewport.GetComponent<LayoutElement>().preferredHeight = 128;
            courseOptionsContent = new GameObject("Course Rows", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            courseOptionsContent.SetParent(viewport.transform, false);
            courseOptionsContent.anchorMin = new Vector2(0, 1);
            courseOptionsContent.anchorMax = Vector2.one;
            courseOptionsContent.pivot = new Vector2(.5f, 1);
            courseOptionsContent.sizeDelta = Vector2.zero;
            var rowsLayout = courseOptionsContent.GetComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 4;
            rowsLayout.padding = new RectOffset(2, 2, 2, 2);
            rowsLayout.childControlWidth = rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;
            courseOptionsContent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = courseOptionsContent;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            coursePopup.SetActive(false);
        }

        // 펼칠 때 선택 상자 아래의 좌표를 계산합니다. 캔버스 배율이 바뀌어도 상자 너비와 맞춥니다.
        private void ToggleCourseDropdown()
        {
            if (coursePopup.activeSelf)
            {
                CloseCourseDropdown();
                return;
            }

            var corners = new Vector3[4];
            ((RectTransform)courseSelectorButton.transform).GetWorldCorners(corners);
            var popupRect = (RectTransform)coursePopup.transform;
            var left = popupRect.InverseTransformPoint(corners[0]);
            var right = popupRect.InverseTransformPoint(corners[3]);
            courseViewport.localPosition = left;
            courseViewport.sizeDelta = new Vector2(right.x - left.x, Mathf.Min(5, screen.UiCourses.Length) * 42 + 4);
            coursePopup.SetActive(true);
            Canvas.ForceUpdateCanvases();
            courseViewport.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
        }

        private void CloseCourseDropdown()
        {
            if (coursePopup != null)
            {
                coursePopup.SetActive(false);
            }
        }

        private Text MakeCourseLabel(string text, Transform parent)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.font = nicknameInput.textComponent.font;
            label.fontSize = 16;
            label.color = nicknameInput.textComponent.color;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.alignment = TextAnchor.MiddleLeft;
            label.text = text;
            return label;
        }

        private void UpdateCourses()
        {
            string key = TrainingJson.Serialize(screen.UiCourses);
            if (key != courseOptionsKey)
            {
                courseOptionsKey = key;
                foreach (var old in courseButtons.Values)
                {
                    old.gameObject.SetActive(false);
                    Destroy(old.gameObject);
                }
                courseButtons.Clear();
                foreach (var course in screen.UiCourses)
                {
                    string id = course.id;
                    var row = new GameObject("Course " + id, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                    row.transform.SetParent(courseOptionsContent, false);
                    row.GetComponent<LayoutElement>().minHeight = 38;
                    row.GetComponent<LayoutElement>().preferredHeight = 38;
                    var button = row.GetComponent<Button>();
                    button.targetGraphic = row.GetComponent<Image>();
                    button.onClick.AddListener(() =>
                    {
                        screen.UiSelectCourse(id);
                        CloseCourseDropdown();
                    });
                    var label = MakeCourseLabel("", row.transform);
                    label.rectTransform.anchorMin = Vector2.zero;
                    label.rectTransform.anchorMax = Vector2.one;
                    label.rectTransform.offsetMin = new Vector2(10, 2);
                    label.rectTransform.offsetMax = new Vector2(-10, -2);
                    courseButtons.Add(id, button);
                }
            }
            courseSelectorButton.interactable = !screen.UiProcessing && screen.UiCourses.Length > 0;
            courseSelectionText.text = "교육과정을 선택해주세요  ▾";
            if (screen.UiProcessing || !loginPanel.activeInHierarchy)
            {
                CloseCourseDropdown();
            }
            foreach (var course in screen.UiCourses)
            {
                var button = courseButtons[course.id];
                bool selected = course.id == screen.UiCourseId;
                if (selected)
                {
                    courseSelectionText.text = course.name + (course.canEnter ? "" : " (입장 제한)") + "  ▾";
                }
                button.GetComponentInChildren<Text>().text = (selected ? "● " : "○ ") + course.name + (course.canEnter ? "" : " (입장 제한)");
                button.GetComponent<Image>().color = selected ? new Color32(191, 219, 254, 255) : new Color32(226, 232, 240, 255);
                button.interactable = !screen.UiProcessing;
            }
        }

        private void UpdateRows()
        {
            string key = screen.UiSelectedLobby;
            foreach (var lobby in screen.UiLobbies)
            {
                key += "|" + JsonUtility.ToJson(lobby);
            }

            if (key == rowsKey)
            {
                return;
            }

            rowsKey = key;
            foreach (var row in rows)
            {
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }

            rows.Clear();
            emptyText.gameObject.SetActive(screen.UiLobbies.Length == 0);
            foreach (var lobby in screen.UiLobbies)
            {
                string id = lobby.id;
                var row = Instantiate(lobbyRowTemplate, lobbyContent);
                row.name = "Lobby - " + id;
                row.gameObject.SetActive(true);
                var card = row.GetComponent<LobbyCardView>();
                if (card != null)
                {
                    card.Render(lobby, id == screen.UiSelectedLobby);
                }
                else
                {
                    var text = row.GetComponentInChildren<Text>();
                    string state = lobby.operatingMode == "scheduled" ? "종료 예정" : lobby.isMaintenance ? "점검 중" : "입장 가능";
                    text.text = (id == screen.UiSelectedLobby ? "● " : "") + lobby.name + "\n" + state + "  ·  사용자 " + lobby.userCount + "  ·  방 " + lobby.roomCount;
                }

                row.onClick.AddListener(() => screen.UiSelect(id));
                rows.Add(row);
            }
        }

        private void OnDestroy()
        {
            if (runtimeFont != null)
            {
                Destroy(runtimeFont);
            }
        }
    }
}
