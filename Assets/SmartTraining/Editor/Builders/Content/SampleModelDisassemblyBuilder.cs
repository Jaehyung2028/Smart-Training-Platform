using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SmartTraining.Editor
{
    // 추가 관찰용 모델에도 기존 드릴과 같은 클립·단계 안내를 연결합니다. 런타임 분해/동기화 코드는 재사용합니다.
    public static class SampleModelDisassemblyBuilder
    {
        public static void Prepare(string prefabPath, bool extinguisher)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var source = root.GetComponent<ModelDisassemblyAnimation>();
                string folder = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
                string clipPath = folder + "/" + root.name + "Disassembly.anim";
                // 사용자가 별도의 분해 클립을 연결한 모델은 자동 생성 설정으로 덮어쓰지 않습니다.
                if (source != null && source.Clip != null && AssetDatabase.GetAssetPath(source.Clip) != clipPath)
                {
                    return;
                }

                var definitions = extinguisher ? ExtinguisherSteps() : SafetyEquipmentSteps();
                foreach (var step in definitions)
                {
                    if (root.transform.Find(step.Path) == null)
                    {
                        Debug.LogWarning("샘플 분해 대상이 없어 기존 프리팹을 유지합니다: " + prefabPath + " / " + step.Path);
                        return;
                    }
                }

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    clip = new AnimationClip
                    {
                        name = root.name + " Disassembly",
                        frameRate = 30,
                        wrapMode = WrapMode.ClampForever
                    };
                    // 다섯 부품을 1초 간격으로 분리합니다. 드릴과 같은 5초 클립 생성 함수를 사용합니다.
                    for (int index = 0; index < definitions.Length; index++)
                    {
                        var step = definitions[index];
                        ModelContentBuilder.AddDisassemblyMove(clip, root, step.Path, step.Offset, index, index + 1);
                    }

                    var settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.loopTime = false;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    AssetDatabase.CreateAsset(clip, clipPath);
                }

                bool changed = false;
                if (source == null)
                {
                    source = root.AddComponent<ModelDisassemblyAnimation>();
                    changed = true;
                }

                var data = new SerializedObject(source);
                if (source.Clip == null)
                {
                    data.FindProperty("disassemblyClip").objectReferenceValue = clip;
                    changed = true;
                }

                var steps = data.FindProperty("steps");
                if (steps.arraySize == 0)
                {
                    steps.arraySize = definitions.Length;
                    for (int index = 0; index < definitions.Length; index++)
                    {
                        var step = steps.GetArrayElementAtIndex(index);
                        step.FindPropertyRelative("time").floatValue = index;
                        step.FindPropertyRelative("title").stringValue = definitions[index].Title;
                        step.FindPropertyRelative("description").stringValue = definitions[index].Description;
                        var targets = step.FindPropertyRelative("targets");
                        targets.arraySize = 1;
                        targets.GetArrayElementAtIndex(0).objectReferenceValue = root.transform.Find(definitions[index].Path);
                    }

                    changed = true;
                }

                data.ApplyModifiedPropertiesWithoutUndo();
                // Animation 창에서도 편집할 수 있도록 Controller를 연결합니다. 실제 실행은 기존 Playables가 평가합니다.
                if (source.Animator.runtimeAnimatorController == null)
                {
                    string controllerPath = folder + "/" + root.name + "Disassembly.controller";
                    var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                    if (controller == null)
                    {
                        controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                        var state = controller.layers[0].stateMachine.AddState("Disassembly");
                        state.motion = clip;
                        EditorUtility.SetDirty(controller);
                    }

                    source.Animator.runtimeAnimatorController = controller;
                    source.Animator.enabled = false;
                    changed = true;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }


        // 실제 정비 절차가 아닌 교육용 구조 표현입니다. 그룹은 유지하고 하위 부품의 위치만 애니메이션합니다.
        private static (string Path, Vector3 Offset, string Title, string Description)[] SafetyEquipmentSteps()
        {
            return new[]
            {
                ("머리 보호 장비/보호 모자", Vector3.up * .6f, "보호 모자 분리", "보호 모자를 위로 띄워 챙과의 위치 관계를 살펴봅니다."),
                ("머리 보호 장비/챙", Vector3.down * .35f, "챙 분리", "아래로 분리된 챙의 형태와 머리 보호 장비의 구성을 확인합니다."),
                ("눈 보호 장비/왼쪽 렌즈", new Vector3(-.45f, 0, .3f), "왼쪽 렌즈 분리", "왼쪽 렌즈를 바깥쪽으로 분리해 연결부와 맞닿는 위치를 살펴봅니다."),
                ("눈 보호 장비/오른쪽 렌즈", new Vector3(.45f, 0, .3f), "오른쪽 렌즈 분리", "오른쪽 렌즈를 분리해 좌우 렌즈의 배치를 비교합니다."),
                ("눈 보호 장비/연결부", new Vector3(0, -.4f, -.3f), "연결부 확인", "렌즈 사이의 연결부를 분리해 전체 구성을 확인합니다. 재조립하면 원래 위치로 돌아갑니다.")
            };
        }


        private static (string Path, Vector3 Offset, string Title, string Description)[] ExtinguisherSteps()
        {
            return new[]
            {
                ("손잡이 조립체/안전핀", Vector3.right * .8f, "안전핀 분리", "안전핀을 옆으로 분리해 손잡이와의 위치 관계를 살펴봅니다. 실제 사용·정비 절차가 아닌 구조 관찰용 표현입니다."),
                ("손잡이 조립체/손잡이", Vector3.up * .65f, "손잡이 분리", "손잡이를 위로 띄워 용기 상단과 연결되는 구조를 확인합니다."),
                ("호스", Vector3.right * .8f, "호스 분리", "호스를 옆으로 분리해 용기와 호스의 배치를 살펴봅니다."),
                ("받침", Vector3.back * .75f, "받침 분리", "앞쪽으로 분리된 받침을 살펴보고 용기 하단의 지지 위치를 확인합니다."),
                ("용기", Vector3.left * .6f, "용기와 전체 구조 확인", "용기를 다른 부품과 분리해 전체 구성을 확인합니다. 재조립하면 분해 과정을 역방향으로 확인할 수 있습니다.")
            };
        }
    }
}
