using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SmartTraining.Editor
{
    public enum UguiPreviewScreen
    {
        Login, LobbyList, LobbyJoin, LobbyInterior, CreateRoom, RoomJoin, Room, RoomSettings, RoomConfirmation, Movement
    }

    [InitializeOnLoad]
    public static class UguiScenePreview
    {
        private static readonly List<Action> restore = new();
        private static Font previewFont;
        public static bool IsActive => restore.Count > 0;
        public static UguiPreviewScreen Current
        {
            get; private set;
        }
        public static UguiSceneLayout Layout
        {
            get; private set;
        }

        static UguiScenePreview()
        {
            EditorSceneManager.sceneSaving += (_, __) => Restore();
            EditorSceneManager.sceneClosing += (_, __) => Restore();
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredEditMode) Restore(); };
            AssemblyReloadEvents.beforeAssemblyReload += Restore;
            Undo.undoRedoPerformed += Restore;
        }

        public static void Show(UguiSceneLayout layout, UguiPreviewScreen screen)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("플레이 종료 후 미리보기를 사용해주세요.");
            Restore();
            var errors = UguiSceneDesigner.Validate(layout);
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join("\n", errors));
            Layout = layout;
            Current = screen;
            bool login = screen <= UguiPreviewScreen.LobbyJoin;
            bool interior = screen >= UguiPreviewScreen.LobbyInterior && screen <= UguiPreviewScreen.RoomJoin;
            SetScreen(layout.loginView, login);
            SetScreen(layout.interiorView, interior);
            SetScreen(layout.roomView, !login && !interior);
            SetActive(layout.loginView.loginPanel, screen == UguiPreviewScreen.Login);
            SetActive(layout.loginView.lobbyPanel, screen == UguiPreviewScreen.LobbyList);
            SetActive(layout.loginView.progressPanel, screen == UguiPreviewScreen.LobbyJoin);
            SetActive(layout.loginView.failurePanel, false);
            SetActive(layout.interiorView.createModal, screen == UguiPreviewScreen.CreateRoom);
            SetActive(layout.interiorView.progressModal, screen == UguiPreviewScreen.RoomJoin);
            SetActive(layout.interiorView.reconnectPanel, false);
            SetGroup(layout.interiorView.mainInteraction, screen != UguiPreviewScreen.CreateRoom && screen != UguiPreviewScreen.RoomJoin);
            SetActive(layout.roomView.settingsModal, screen == UguiPreviewScreen.RoomSettings);
            SetActive(layout.roomView.confirmationModal, screen == UguiPreviewScreen.RoomConfirmation);
            SetActive(layout.roomView.mapModal, screen == UguiPreviewScreen.Movement);
            SetGroup(layout.roomView.mainInteraction, screen == UguiPreviewScreen.Room);
            if (screen == UguiPreviewScreen.RoomConfirmation)
            {
                var label = layout.roomView.confirmationText;
                Set(label, () => label.text, value => label.text = value, "방 관리 요청 확인 영역입니다.\n실행 시 대상과 작업에 맞는 안내가 표시됩니다.");
            }
            Canvas.ForceUpdateCanvases();
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void SetScreen(Component view, bool visible)
        {
            var canvas = view.GetComponent<Canvas>();
            var raycaster = view.GetComponent<GraphicRaycaster>();
            Set(canvas, () => canvas.enabled, value => canvas.enabled = value, visible);
            // Preview never wires network actions or receives button input.
            Set(raycaster, () => raycaster.enabled, value => raycaster.enabled = value, false);
            SetGroup(view.GetComponent<CanvasGroup>(), visible);
            if (!visible)
                return;
            Font font = view is LobbyUguiView login ? login.fontOverride : view is LobbyInteriorUguiView interior ? interior.fontOverride : ((RoomUguiView)view).fontOverride;
            if (font == null)
            {
                if (previewFont == null)
                {
                    var names = new HashSet<string>(Font.GetOSInstalledFontNames(), StringComparer.OrdinalIgnoreCase);
                    foreach (var name in new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR" })
                        if (names.Contains(name))
                        {
                            previewFont = Font.CreateDynamicFontFromOSFont(name, 20);
                            previewFont.hideFlags = HideFlags.HideAndDontSave;
                            break;
                        }
                }
                font = previewFont;
            }
            if (font != null)
            foreach (var label in view.GetComponentsInChildren<Text>(true))
                Set(label, () => label.font, value => label.font = value, font);
        }

        private static void SetActive(GameObject root, bool active) => Set(root, () => root.activeSelf, value => root.SetActive(value), active);
        private static void SetGroup(CanvasGroup group, bool active)
        {
            Set(group, () => group.interactable, value => group.interactable = value, active);
            Set(group, () => group.blocksRaycasts, value => group.blocksRaycasts = value, active);
        }
        private static void Set<T>(Object target, Func<T> read, Action<T> write, T preview)
        {
            var original = read();
            restore.Add(() => { if (target != null && EqualityComparer<T>.Default.Equals(read(), preview)) write(original); });
            write(preview);
        }

        public static void Restore()
        {
            for (int i = restore.Count - 1; i >= 0; i--)
                restore[i]();
            restore.Clear();
            Layout = null;
            if (previewFont != null)
                Object.DestroyImmediate(previewFont);
            previewFont = null;
            SceneView.RepaintAll();
        }
    }

    public sealed class UguiSceneBuildCheck : IPreprocessBuildWithReport, IProcessSceneWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => UguiScenePreview.Restore();
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            foreach (var layout in UguiSceneDesigner.InScene<UguiSceneLayout>(scene))
            {
                var errors = UguiSceneDesigner.Validate(layout);
                if (errors.Count > 0)
                    throw new BuildFailedException("씬 UI 연결 오류:\n" + string.Join("\n", errors));
            }
        }
    }
}
