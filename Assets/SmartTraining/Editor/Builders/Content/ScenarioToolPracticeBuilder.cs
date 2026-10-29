using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    // 기존 기본 예시만 공구별 6단계로 확장합니다. 직접 편집한 단계와 이미 배치한 대상은 보존합니다.
    public static class ScenarioToolPracticeBuilder
    {
        public static void PrepareDefinition(ScenarioDefinition definition)
        {
            if (definition.Id != "tool-assembly" ||
                definition.Steps.Count != 3 ||
                definition.Steps[0].Id != "loosen-first" ||
                definition.Steps[1].Id != "tighten-first" ||
                definition.Steps[2].Id != "inspect-result")
            {
                return;
            }

            var data = new SerializedObject(definition);
            data.FindProperty("title").stringValue = "공구별 체결·타격 실습";
            var steps = data.FindProperty("steps");
            steps.arraySize = 6;
            Step(
                steps.GetArrayElementAtIndex(2),
                "loosen-screw",
                "드라이버로 나사 풀기",
                "드라이버를 선택하고 왼쪽 나사를 푸세요.",
                "screwdriver",
                "screw-a",
                ScenarioAction.Loosen,
                1.8f
            );
            Step(
                steps.GetArrayElementAtIndex(3),
                "tighten-screw",
                "드라이버로 나사 체결",
                "같은 나사를 드라이버로 다시 체결하세요.",
                "screwdriver",
                "screw-a",
                ScenarioAction.Tighten,
                1.8f
            );
            Step(
                steps.GetArrayElementAtIndex(4),
                "strike-pin",
                "망치로 핀 고정",
                "망치를 선택하고 오른쪽 핀을 세 번 두드려 고정하세요.",
                "hammer",
                "pin-a",
                ScenarioAction.Strike,
                2.4f
            );
            Step(
                steps.GetArrayElementAtIndex(5),
                "inspect-result",
                "전체 작업 결과 확인",
                "공구 없이 고정한 핀을 클릭해 작업 결과를 확인하세요.",
                "",
                "pin-a",
                ScenarioAction.Check,
                0
            );
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }


        private static void Step(
            SerializedProperty step,
            string id,
            string title,
            string instruction,
            string toolId,
            string targetId,
            ScenarioAction action,
            float duration
        )
        {
            step.FindPropertyRelative("id").stringValue = id;
            step.FindPropertyRelative("title").stringValue = title;
            step.FindPropertyRelative("instruction").stringValue = instruction;
            step.FindPropertyRelative("toolId").stringValue = toolId;
            step.FindPropertyRelative("targetId").stringValue = targetId;
            step.FindPropertyRelative("action").enumValueIndex = (int)action;
            step.FindPropertyRelative("durationSeconds").floatValue = duration;
        }


        public static void PrepareTargets(GameObject root)
        {
            string path = ScenarioToolPrefabBuilder.Folder + "/TargetDetail.mat";
            var detail = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (detail == null)
            {
                detail = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    name = "Target Detail"
                };
                detail.SetColor("_BaseColor", new Color(.08f, .1f, .13f));
                AssetDatabase.CreateAsset(detail, path);
            }

            var metal = AssetDatabase.LoadAssetAtPath<Material>(ScenarioContentBuilder.DirectoryPath + "/ScenarioParts.mat");
            CreateTarget(
                root.transform,
                "Screw A",
                "screw-a",
                new Vector3(-.7f, .97f, 4),
                new Vector3(.04f, .07f, .04f),
                new Vector3(0, .1f, 0),
                2,
                metal,
                detail,
                true
            );
            CreateTarget(
                root.transform,
                "Pin A",
                "pin-a",
                new Vector3(.7f, .99f, 4),
                new Vector3(.045f, .09f, .045f),
                new Vector3(0, -.18f, 0),
                0,
                metal,
                detail,
                false
            );
            // 기존 오답용 원통은 새 핀과 겹치지 않도록 기본 배치일 때만 뒤로 옮깁니다.
            var old = root.transform.Find("Bolt B");
            if (old != null && old.localPosition == new Vector3(.7f, 1.02f, 4))
            {
                old.localPosition = new Vector3(1.05f, 1.02f, 4.35f);
            }
        }


        private static void CreateTarget(
            Transform parent,
            string name,
            string id,
            Vector3 position,
            Vector3 scale,
            Vector3 offset,
            float turns,
            Material metal,
            Material detail,
            bool screw
        )
        {
            if (parent.Find(name) != null)
            {
                return;
            }

            var target = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            target.name = name;
            target.transform.SetParent(parent, false);
            target.transform.localPosition = position;
            target.transform.localScale = scale;
            target.GetComponent<Renderer>().sharedMaterial = metal;
            var marker = target.AddComponent<ScenarioObject>();
            var data = new SerializedObject(marker);
            data.FindProperty("id").stringValue = id;
            data.FindProperty("kind").enumValueIndex = (int)ScenarioObjectKind.Target;
            data.FindProperty("loosenOffset").vector3Value = offset;
            data.FindProperty("loosenTurns").floatValue = turns;
            var head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            head.name = "Head";
            head.transform.SetParent(target.transform, false);
            head.transform.localPosition = new Vector3(0, 1.2f, 0);
            head.transform.localScale = new Vector3(1.65f, .2f, 1.65f);
            head.GetComponent<Renderer>().sharedMaterial = metal;
            var anchor = new GameObject("Work Anchor").transform;
            anchor.SetParent(target.transform, false);
            anchor.localPosition = new Vector3(0, 1.4f, 0);
            data.FindProperty("workAnchor").objectReferenceValue = anchor;
            data.ApplyModifiedPropertiesWithoutUndo();
            if (screw)
            {
                for (int index = 0; index < 2; index++)
                {
                    var slot = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    slot.name = "Cross Slot " + index;
                    slot.transform.SetParent(target.transform, false);
                    slot.transform.localPosition = new Vector3(0, 1.405f, 0);
                    slot.transform.localScale = index == 0 ? new Vector3(1.25f, .03f, .18f) : new Vector3(.18f, .03f, 1.25f);
                    slot.GetComponent<Renderer>().sharedMaterial = detail;
                    Object.DestroyImmediate(slot.GetComponent<Collider>());
                }
            }
        }
    }
}
