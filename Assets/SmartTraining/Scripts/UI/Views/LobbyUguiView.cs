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
            runtimeFont = UguiViewUtility.ApplyFont(gameObject, fontOverride);
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
                loginPanel.SetActive(false);
                lobbyPanel.SetActive(false);
                progressPanel.SetActive(false);
                failurePanel.SetActive(false);
                progressText.text = failureText.text = "";
                return;
            }

            bool processing = screen.UiProcessing, loggedIn = screen.UiLoggedIn;
            bool joining = !string.IsNullOrEmpty(screen.UiProgress);
            loginPanel.SetActive(!loggedIn && !joining);
            lobbyPanel.SetActive(loggedIn && !joining);
            progressPanel.SetActive(joining);
            failurePanel.SetActive(loggedIn && !joining && !string.IsNullOrEmpty(screen.UiFailure));
            addressInput.SetTextWithoutNotify(screen.UiAddress);
            nicknameInput.SetTextWithoutNotify(screen.UiNickname);
            addressInput.interactable = nicknameInput.interactable = !processing && !loggedIn;
            healthButton.interactable = loginButton.interactable = saveButton.interactable = resetButton.interactable = !processing;
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
