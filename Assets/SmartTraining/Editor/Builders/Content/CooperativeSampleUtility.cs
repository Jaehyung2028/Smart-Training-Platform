using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    // 샘플 생성기끼리 공유하는 편집기 전용 단계·도형 생성 함수입니다.
    internal static class CooperativeSampleUtility
    {
        internal static void Step(
            SerializedProperty item,
            string id,
            string title,
            string instruction,
            string tool,
            string target,
            ScenarioRole role,
            ScenarioAction action
        )
        {
            item.FindPropertyRelative("id").stringValue = id;
            item.FindPropertyRelative("title").stringValue = title;
            item.FindPropertyRelative("instruction").stringValue = instruction;
            item.FindPropertyRelative("toolId").stringValue = tool;
            item.FindPropertyRelative("targetId").stringValue = target;
            item.FindPropertyRelative("requiredRole").intValue = (int)role;
            item.FindPropertyRelative("action").enumValueIndex = (int)action;
            item.FindPropertyRelative("durationSeconds").floatValue = 1.5f;
        }


        internal static void Change(SerializedProperty item, int steps, GameObject before, GameObject after)
        {
            item.FindPropertyRelative("completedSteps").intValue = steps;
            item.FindPropertyRelative("before").objectReferenceValue = before;
            item.FindPropertyRelative("after").objectReferenceValue = after;
        }


        internal static Transform Target(Transform parent, string name, string id, Vector3 position)
        {
            var target = new GameObject(name, typeof(ScenarioObject), typeof(BoxCollider));
            target.transform.SetParent(parent, false);
            target.transform.localPosition = position;
            target.GetComponent<BoxCollider>().size = new Vector3(.4f, .35f, .18f);
            var data = new SerializedObject(target.GetComponent<ScenarioObject>());
            data.FindProperty("id").stringValue = id;
            data.FindProperty("kind").enumValueIndex = (int)ScenarioObjectKind.Target;
            data.FindProperty("loosenOffset").vector3Value = Vector3.zero;
            data.ApplyModifiedPropertiesWithoutUndo();
            return target.transform;
        }


        internal static GameObject Part(
            Transform parent,
            string name,
            PrimitiveType type,
            Vector3 position,
            Vector3 scale,
            Material material
        )
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            return part;
        }


        internal static Material Material(string folder, string name, Color color)
        {
            string path = folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    color = color
                };
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
        }


        internal static void Label(Transform parent, string text, Vector3 position)
        {
            var label = new GameObject(text, typeof(TextMesh)).GetComponent<TextMesh>();
            label.transform.SetParent(parent, false);
            label.transform.localPosition = position;
            label.text = text;
            label.fontSize = 64;
            label.characterSize = .009f;
            label.anchor = TextAnchor.MiddleCenter;
        }
    }
}
