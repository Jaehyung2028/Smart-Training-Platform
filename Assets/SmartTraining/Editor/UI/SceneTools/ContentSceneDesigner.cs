using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmartTraining.Editor
{
    // 명시적으로 실행할 때 누락된 씬 요소만 배치합니다. 기존 맵·참조·배치를 덮어쓰지 않습니다.
    public static class ContentSceneDesigner
    {
        [MenuItem("Smart Training/Content/Place Missing Content Scene Objects")]
        public static void PlaceInCurrentScene()
        {
            Install(SceneManager.GetActiveScene());
        }


        public static ContentSceneLayout Install(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ContentSceneSession.ScenePath)
            {
                throw new InvalidOperationException("플레이를 종료하고 TrainingContent 씬을 열어주세요.");
            }

            var layout = UguiSceneDesigner.InScene<ContentSceneLayout>(scene).SingleOrDefault();
            if (layout == null)
            {
                var root = new GameObject("Content Scene");
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "콘텐츠 씬 배치");
                layout = Undo.AddComponent<ContentSceneLayout>(root);
            }

            var serialized = new SerializedObject(layout);
            EnsurePrefab(
                serialized,
                "playerCamera",
                "Assets/SmartTraining/Resources/SmartTraining/FirstPersonCamera.prefab",
                layout.transform
            );
            EnsurePrefab(
                serialized,
                "hud",
                "Assets/SmartTraining/Resources/SmartTraining/UI/Templates/ContentHudController.prefab",
                layout.transform
            );
            var resources = serialized.FindProperty("resources");
            if (resources.objectReferenceValue == null)
            {
                var root = new GameObject("Content Resources");
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.SetParent(layout.transform, false);
                Undo.RegisterCreatedObjectUndo(root, "콘텐츠 배치 기준점");
                resources.objectReferenceValue = Undo.AddComponent<ContentResourceContext>(root);
            }

            serialized.ApplyModifiedProperties();
            layout.ValidateReferences();
            EditorSceneManager.MarkSceneDirty(scene);
            return layout;
        }


        private static void EnsurePrefab(SerializedObject layout, string property, string path, Transform parent)
        {
            var reference = layout.FindProperty(property);
            if (reference.objectReferenceValue != null)
            {
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            var ui = instance.GetComponent<UiPrefabReferences>();
            if (ui != null && !string.IsNullOrEmpty(ui.InstanceName))
            {
                instance.name = ui.InstanceName;
            }

            Undo.RegisterCreatedObjectUndo(instance, "콘텐츠 씬 프리팹 배치");
            reference.objectReferenceValue = property == "hud" ? (UnityEngine.Object)instance.GetComponent<UiPrefabReferences>() : instance.GetComponent<FirstPersonCameraController>();
            // 입장 준비가 끝나면 세션이 켭니다. 편집 시에는 필요에 따라 켜서 배치를 확인할 수 있습니다.
            instance.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
        }
    }
}
