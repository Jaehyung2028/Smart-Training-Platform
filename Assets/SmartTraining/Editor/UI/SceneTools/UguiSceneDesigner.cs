using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SmartTraining.Editor
{
    public static class UguiSceneDesigner
    {
        public const string ClientScene = "Assets/SmartTraining/Scenes/ApiLobby.unity";

        public static T[] InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        [MenuItem("Smart Training/UI/Scene UI Editor")]
        public static void OpenWindow() => EditorWindow.GetWindow<UguiSceneWindow>("UI 편집");

        [MenuItem("Smart Training/UI/Place UI in Current Scene")]
        public static void PlaceInCurrentScene() => Install(SceneManager.GetActiveScene());

        public static UguiSceneLayout Install(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("플레이 종료 후 UI를 배치해주세요.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("프리팹 편집을 닫고 ApiLobby 씬을 열어주세요.");
            var controllers = InScene<ApiLobbyScreen>(scene);
            if (controllers.Length != 1) throw new InvalidOperationException("현재 씬에 ApiLobbyScreen이 정확히 하나 있어야 합니다.");
            var controller = controllers[0];
            if (controller.GetComponentsInChildren<UguiSceneLayout>(true).Length > 1 ||
                controller.GetComponentsInChildren<LobbyUguiView>(true).Length > 1 ||
                controller.GetComponentsInChildren<LobbyInteriorUguiView>(true).Length > 1 ||
                controller.GetComponentsInChildren<RoomUguiView>(true).Length > 1)
                throw new InvalidOperationException("중복 UI가 있습니다. 연결 검사 결과를 확인해주세요.");
            UguiScenePreview.Restore();
            var layout = controller.GetComponentInChildren<UguiSceneLayout>(true);
            bool created = layout == null;
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("씬 UI 배치");
            if (layout == null)
            {
                var root = new GameObject("UI Workspace"); SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "씬 UI 배치"); Undo.SetTransformParent(root.transform, controller.transform, "씬 UI 배치");
                layout = Undo.AddComponent<UguiSceneLayout>(root);
            }
            Undo.RecordObject(layout, "UI 연결"); layout.controller = controller;
            layout.loginView = EnsureView<LobbyUguiView>(controller, layout.transform, LobbyUguiBuilder.AssetPath);
            layout.interiorView = EnsureView<LobbyInteriorUguiView>(controller, layout.transform, LobbyInteriorUguiBuilder.InteriorPath);
            layout.roomView = EnsureView<RoomUguiView>(controller, layout.transform, RoomUguiBuilder.AssetPath);
            var events = InScene<EventSystem>(scene);
            if (events.Length == 0)
            {
                var root = new GameObject("UI Event System"); SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "UI Event System"); Undo.SetTransformParent(root.transform, layout.transform, "UI Event System");
                layout.eventSystem = Undo.AddComponent<EventSystem>(root); Undo.AddComponent<InputSystemUIInputModule>(root);
            }
            else layout.eventSystem = events[0];
            if (created)
            {
                SetInitial(layout.loginView, true); SetInitial(layout.interiorView, false); SetInitial(layout.roomView, false);
            }
            EditorUtility.SetDirty(layout); EditorSceneManager.MarkSceneDirty(scene); Undo.CollapseUndoOperations(undo);
            Selection.activeGameObject = layout.gameObject;
            return layout;
        }

        private static T EnsureView<T>(ApiLobbyScreen controller, Transform parent, string path) where T : Component
        {
            var existing = controller.GetComponentInChildren<T>(true);
            if (existing != null) return existing;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("UI 프리팹을 찾을 수 없습니다: " + path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, controller.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(instance, "UI 프리팹 배치"); Undo.SetTransformParent(instance.transform, parent, "UI 프리팹 배치");
            return instance.GetComponent<T>();
        }

        private static void SetInitial(Component view, bool visible)
        {
            var canvas = view.GetComponent<Canvas>(); var group = view.GetComponent<CanvasGroup>(); var raycaster = view.GetComponent<GraphicRaycaster>();
            Undo.RecordObjects(new Object[] {canvas,group,raycaster}, "초기 UI 표시");
            canvas.enabled = visible; group.interactable = group.blocksRaycasts = visible; raycaster.enabled = visible;
            foreach (var component in new Component[] {canvas,group,raycaster}) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }

        public static List<string> Validate(UguiSceneLayout layout)
        {
            var errors = new List<string>();
            if (layout == null) { errors.Add("씬에 UI Workspace가 없습니다. '씬에 UI 배치'를 눌러주세요."); return errors; }
            if (layout.controller == null || !layout.transform.IsChildOf(layout.controller.transform)) errors.Add("Workspace.controller: 부모 ApiLobbyScreen 연결을 확인해주세요.");
            if (layout.loginView == null) errors.Add("Workspace.loginView: 로그인 화면 연결이 없습니다.");
            if (layout.interiorView == null) errors.Add("Workspace.interiorView: 로비 내부 화면 연결이 없습니다.");
            if (layout.roomView == null) errors.Add("Workspace.roomView: 방 내부 화면 연결이 없습니다.");
            if (layout.eventSystem == null || !layout.eventSystem.gameObject.activeInHierarchy || layout.eventSystem.GetComponent<InputSystemUIInputModule>() == null)
                errors.Add("Workspace.eventSystem: 활성 EventSystem과 InputSystemUIInputModule이 필요합니다.");
            var scene = layout.gameObject.scene;
            if (InScene<ApiLobbyScreen>(scene).Length != 1) errors.Add("ApiLobbyScreen이 중복되어 있습니다.");
            if (InScene<LobbyUguiView>(scene).Length != 1 || InScene<LobbyInteriorUguiView>(scene).Length != 1 || InScene<RoomUguiView>(scene).Length != 1)
                errors.Add("로그인·로비·방 화면은 각각 하나씩 있어야 합니다.");
            if (InScene<EventSystem>(scene).Length != 1) errors.Add("EventSystem은 씬에 하나만 두어주세요.");
            foreach (var view in new Component[] {layout.loginView, layout.interiorView, layout.roomView})
            {
                if (view == null) continue;
                if (layout.controller != null && !view.transform.IsChildOf(layout.controller.transform)) errors.Add(view.name + ": 화면은 같은 ApiLobbyScreen 아래에 배치해주세요.");
                if (!view.gameObject.activeInHierarchy) errors.Add(view.name + ": 화면 오브젝트는 활성화하고 Canvas로 표시를 전환해주세요.");
                if (view.GetComponent<Canvas>() == null || view.GetComponent<CanvasGroup>() == null || view.GetComponent<GraphicRaycaster>() == null)
                    errors.Add(view.name + ": Canvas·CanvasGroup·GraphicRaycaster 연결이 필요합니다.");
                foreach (var node in view.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) > 0) errors.Add(node.name + ": Missing Script가 있습니다.");
                foreach (var field in view.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    if (field.Name == "fontOverride") continue;
                    if (typeof(Object).IsAssignableFrom(field.FieldType)) CheckReference(field.GetValue(view) as Object, view, field.Name, errors);
                    else if (field.FieldType.IsArray && typeof(Object).IsAssignableFrom(field.FieldType.GetElementType()))
                    {
                        var array = field.GetValue(view) as Array;
                        if (array == null || array.Length == 0) errors.Add(view.name + "." + field.Name + ": 목록 연결이 비어 있습니다.");
                        else for (int i=0;i<array.Length;i++) CheckReference(array.GetValue(i) as Object, view, field.Name + "[" + i + "]", errors);
                    }
                }
            }
            return errors;
        }

        private static void CheckReference(Object value, Component view, string field, List<string> errors)
        {
            if (value == null) { errors.Add(view.name + "." + field + ": 연결이 없습니다."); return; }
            var transform = value is Component component ? component.transform : value is GameObject root ? root.transform : null;
            if (transform != null && !transform.IsChildOf(view.transform)) errors.Add(view.name + "." + field + ": 다른 화면의 오브젝트가 연결되어 있습니다.");
        }

        // Used only by the isolated preparation project; normal editor tools never save automatically.
        public static void PrepareClientScene()
        {
            LobbyUguiBuilder.EnsureAssets(); LobbyInteriorUguiBuilder.EnsureAssets(); RoomUguiBuilder.EnsureAssets();
            var scene = EditorSceneManager.OpenScene(ClientScene);
            var layout = Install(scene); var errors = Validate(layout);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            EditorSceneManager.SaveScene(scene);
            Debug.Log("SCENE UI PREPARED: linked prefab instances and one EventSystem");
        }
    }
}
