using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmartTraining.Editor
{
    public static class ModelPartHighlightChecks
    {
        [MenuItem("Smart Training/Content/Check Model Part Highlight")]
        public static void Run()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Highlight Check");
            SceneManager.MoveGameObjectToScene(root, scene);
            Material material = null;
            AnimationClip animation = null;
            ModelPartHighlighter highlighter = null;
            try
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                int colorId = Shader.PropertyToID(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color");
                material.SetColor(colorId, Color.yellow);
                var sphere = Part(root, PrimitiveType.Sphere, Vector3.zero, material);
                var cube = Part(root, PrimitiveType.Cube, new Vector3(0, 0, 1.5f), material);
                var original = new MaterialPropertyBlock();
                original.SetColor(colorId, Color.red);
                original.SetFloat("_SavedValue", 0.37f);
                cube.SetPropertyBlock(original, 0);
                highlighter = new ModelPartHighlighter(new Renderer[] { sphere, cube });
                var centerRay = new Ray(new Vector3(0, 0, -3), Vector3.forward);
                highlighter.UpdateHover(centerRay, true, 6);
                Check(highlighter.Highlighted == sphere, "가장 가까운 부품을 선택합니다.");
                Check(material.GetColor(colorId) == Color.yellow, "공유 재질의 색상을 변경하지 않습니다.");

                // 구의 경계 상자에는 들어가지만 실제 구 표면을 지나지 않는 경로입니다.
                var cornerRay = new Ray(new Vector3(0.45f, 0.45f, -3), Vector3.forward);
                highlighter.UpdateHover(cornerRay, true, 6);
                Check(highlighter.Highlighted == cube, "빈 경계 영역을 부품으로 오인하지 않습니다.");
                var highlighted = new MaterialPropertyBlock();
                cube.GetPropertyBlock(highlighted, 0);
                Check(highlighted.GetColor(colorId) != Color.red, "해당 부품에만 강조 색상을 적용합니다.");
                var restored = new MaterialPropertyBlock();
                sphere.GetPropertyBlock(restored, 0);
                Check(restored.isEmpty, "이전 부품의 임시 강조를 제거합니다.");

                highlighter.UpdateHover(cornerRay, false, 6);
                cube.GetPropertyBlock(restored, 0);
                Check(highlighter.Highlighted == null && restored.GetColor(colorId) == Color.red &&
                    Mathf.Approximately(restored.GetFloat("_SavedValue"), 0.37f), "입력이 차단되면 기존 슬롯 설정을 복원합니다.");
                highlighter.UpdateHover(centerRay, true, 1);
                Check(highlighter.Highlighted == null, "범위 밖의 모델은 강조하지 않습니다.");
                highlighter.UpdateHover(centerRay, true, 6);
                var orange = new Color(1, 0.55f, 0.1f, 1);
                highlighter.SetSelected(cube);
                cube.GetPropertyBlock(highlighted, 0);
                Check(highlighter.IsSelected(cube) && highlighted.GetColor(colorId) == orange, "선택한 부품은 주황색으로 표시합니다.");
                highlighter.ClearHover();
                cube.GetPropertyBlock(highlighted, 0);
                Check(highlighted.GetColor(colorId) == orange, "마우스 강조 해제 후에도 선택 색상을 유지합니다.");
                highlighter.UpdateHover(centerRay, true, 6);
                sphere.GetPropertyBlock(restored, 0);
                cube.GetPropertyBlock(highlighted, 0);
                Check(restored.GetColor(colorId) == new Color(0.25f, 0.9f, 1f, 1) && highlighted.GetColor(colorId) == orange,
                    "다른 부품의 마우스 강조와 선택 강조를 동시에 표시합니다.");
                highlighter.UpdateHover(cornerRay, true, 6);
                cube.GetPropertyBlock(highlighted, 0);
                Check(highlighted.GetColor(colorId) == orange, "동일 부품에서는 선택 색상이 우선합니다.");
                highlighter.ClearHover();
                highlighter.SetSelected(sphere);
                cube.GetPropertyBlock(restored, 0);
                sphere.GetPropertyBlock(highlighted, 0);
                Check(restored.GetColor(colorId) == Color.red && highlighted.GetColor(colorId) == orange, "선택을 옮기면 이전 부품의 원래 색상을 복원합니다.");
                highlighter.SetSelected(null);
                sphere.GetPropertyBlock(restored, 0);
                Check(restored.isEmpty && highlighter.SelectedCount == 0, "선택 해제 시 임시 슬롯 설정을 제거합니다.");
                highlighter.SetSelected(cube);
                highlighter.Dispose();
                highlighter.Dispose();
                cube.GetPropertyBlock(restored, 0);
                Check(restored.GetColor(colorId) == Color.red && Mathf.Approximately(restored.GetFloat("_SavedValue"), 0.37f),
                    "종료 시 선택 색상과 기존 슬롯 값을 복원합니다.");
                Check(sphere.GetComponent<Collider>() == null && cube.GetComponent<Collider>() == null, "생성한 선택용 Collider를 정리합니다.");
                sphere.GetPropertyBlock(restored, 0);
                Check(restored.isEmpty, "종료 시 강조 설정도 정리합니다.");
                var gesture = new ModelPointerGesture();
                gesture.Begin(Vector2.zero, 1);
                Check(gesture.End(new Vector2(2, 2), 1.1f), "짧은 클릭과 작은 손 떨림을 허용합니다.");
                gesture.Begin(Vector2.zero, 1);
                gesture.Update(new Vector2(9, 0));
                gesture.Update(Vector2.zero);
                Check(gesture.IsDragging && !gesture.End(Vector2.zero, 1.2f), "드래그 후 원점으로 돌아와도 클릭하지 않습니다.");
                gesture.Begin(Vector2.zero, 1);
                Check(!gesture.End(Vector2.zero, 1.5f), "긴 누르기는 클릭 설명을 열지 않습니다.");
                gesture.Begin(Vector2.zero, 1);
                gesture.Cancel();
                Check(!gesture.End(Vector2.zero, 1.1f), "입력이 취소되면 클릭도 취소됩니다.");
                gesture.Begin(Vector2.zero, 1);
                Check(gesture.End(new Vector2(8, 0), 1.3f), "드래그 임계값의 경계를 일관되게 처리합니다.");
                var info = root.AddComponent<ModelPartInfo>();
                Check(info.Title == root.name && info.Description.Length > 0, "설명이 없는 모델도 기본 안내를 표시합니다.");
                var data = new SerializedObject(info);
                data.FindProperty("title").stringValue = "배터리";
                data.FindProperty("description").stringValue = "공구에 전원을 공급합니다.";
                data.ApplyModifiedPropertiesWithoutUndo();
                Check(cube.GetComponentInParent<ModelPartInfo>().Title == "배터리" && info.Description.Contains("전원"), "부모의 부품 설명 데이터도 조회할 수 있습니다.");
                var assembly = new GameObject("Battery Assembly").transform;
                assembly.SetParent(root.transform, false);
                var nested = new GameObject("Cells").transform;
                nested.SetParent(assembly, false);
                sphere.transform.SetParent(assembly, true);
                cube.transform.SetParent(nested, true);
                var empty = new GameObject("Empty Helper").transform;
                empty.SetParent(root.transform, false);
                var groupInfo = assembly.gameObject.AddComponent<ModelPartInfo>();
                var groupData = new SerializedObject(groupInfo);
                groupData.FindProperty("title").stringValue = "배터리 조립체";
                groupData.FindProperty("description").stringValue = "배터리 하위 구조입니다.";
                groupData.ApplyModifiedPropertiesWithoutUndo();
                var tree = new ModelPartTree(root.transform, new Renderer[] { sphere, cube });
                Check(tree.Find(assembly) != null && tree.Find(cube.transform).Depth == 3 && tree.Find(empty) == null,
                    "깊은 Transform 계층을 유지하고 빈 보조 가지는 제외합니다.");
                Check(tree.Find(assembly).Renderers.Count == 2 && tree.Find(nested).Renderers.Count == 1,
                    "렌더러가 없는 부모도 하위 부품 묶음으로 조회합니다.");
                Check(tree.Find(assembly).Title == "배터리 조립체" && tree.Find(cube.transform).Title == cube.name &&
                    tree.Find(cube.transform).Description == groupInfo.Description, "선택 노드의 이름과 설명을 구분합니다.");
                highlighter = new ModelPartHighlighter(new Renderer[] { sphere, cube });
                highlighter.SetSelection(tree.Find(assembly).Renderers);
                sphere.GetPropertyBlock(highlighted, 0);
                cube.GetPropertyBlock(restored, 0);
                Check(highlighter.SelectedCount == 2 && highlighted.GetColor(colorId) == orange && restored.GetColor(colorId) == orange,
                    "부모를 선택하면 모든 하위 렌더러를 강조합니다.");
                highlighter.UpdateHover(centerRay, true, 6);
                sphere.GetPropertyBlock(highlighted, 0);
                Check(highlighted.GetColor(colorId) == orange, "묶음 내부의 마우스 강조보다 선택 색상이 우선합니다.");
                highlighter.ClearHover();
                highlighter.SetSelection(tree.Find(cube.transform).Renderers);
                sphere.GetPropertyBlock(highlighted, 0);
                Check(highlighter.SelectedCount == 1 && highlighted.isEmpty, "자식으로 선택을 옮기면 형제의 색상을 복원합니다.");
                highlighter.SetSelection(tree.Find(assembly).Renderers);
                highlighter.Clear();
                sphere.GetPropertyBlock(highlighted, 0);
                cube.GetPropertyBlock(restored, 0);
                Check(highlighted.isEmpty && restored.GetColor(colorId) == Color.red &&
                    Mathf.Approximately(restored.GetFloat("_SavedValue"), 0.37f), "부모·자식 선택이 겹쳐도 원래 슬롯 설정을 복원합니다.");
                highlighter.SetSelected(cube);
                highlighter.SetSurroundingsTransparency(true, 0.3f);
                sphere.GetPropertyBlock(highlighted, 0);
                Check(sphere.sharedMaterial != material && Mathf.Approximately(highlighted.GetColor(colorId).a, 0.3f),
                    "선택 밖의 부품만 임시 재질로 반투명 처리합니다.");
                Check(cube.sharedMaterial == material && material.GetColor(colorId) == Color.yellow,
                    "선택 부품과 원본 재질은 변경하지 않습니다.");
                highlighter.UpdateHover(centerRay, true, 6);
                sphere.GetPropertyBlock(highlighted, 0);
                Check(Mathf.Approximately(highlighted.GetColor(colorId).a, 0.3f), "마우스 강조 중에도 투명도를 유지합니다.");
                highlighter.SetSurroundingsTransparency(true, 0.65f);
                sphere.GetPropertyBlock(highlighted, 0);
                Check(Mathf.Approximately(highlighted.GetColor(colorId).a, 0.65f), "투명도 조절은 누적되지 않습니다.");
                highlighter.SetSelection(tree.Find(assembly).Renderers);
                Check(sphere.sharedMaterial == material && cube.sharedMaterial == material,
                    "부모를 선택하면 자식들의 원래 불투명도를 복원합니다.");
                highlighter.SetSelected(cube);
                highlighter.SetSurroundingsTransparency(false, 0.65f);
                Check(sphere.sharedMaterial == material, "기능을 끄면 원래 재질로 복구합니다.");
                highlighter.SetSurroundingsTransparency(true, 0);
                highlighter.Clear();
                sphere.GetPropertyBlock(highlighted, 0);
                cube.GetPropertyBlock(restored, 0);
                Check(sphere.sharedMaterial == material && highlighted.isEmpty && restored.GetColor(colorId) == Color.red &&
                    Mathf.Approximately(restored.GetFloat("_SavedValue"), 0.37f), "선택 해제 시 원래 재질과 슬롯 설정을 복원합니다.");
                var sphereStart = sphere.transform.localPosition;
                var cubeStart = cube.transform.localPosition;
                var cubeRotation = cube.transform.localRotation;
                var cubeScale = cube.transform.localScale;
                var source = root.AddComponent<ModelDisassemblyAnimation>();
                using (var missing = new ModelExplodedView(source))
                {
                    Check(!missing.IsAvailable, "클립이 없으면 자동 분해로 대체하지 않습니다.");
                }
                animation = new AnimationClip { name = "Disassembly Check", wrapMode = WrapMode.ClampForever };
                string cubePath = AnimationUtility.CalculateTransformPath(cube.transform, root.transform);
                string spherePath = AnimationUtility.CalculateTransformPath(sphere.transform, root.transform);
                AnimationUtility.SetEditorCurve(animation, EditorCurveBinding.FloatCurve(cubePath, typeof(Transform), "m_LocalPosition.y"),
                    AnimationCurve.Linear(0, cubeStart.y, 4, cubeStart.y + 2));
                AnimationUtility.SetEditorCurve(animation, EditorCurveBinding.FloatCurve(spherePath, typeof(Transform), "m_LocalPosition.x"),
                    new AnimationCurve(new Keyframe(0, sphereStart.x), new Keyframe(2, sphereStart.x), new Keyframe(4, sphereStart.x + 1)));
                AnimationUtility.SetEditorCurve(animation, EditorCurveBinding.FloatCurve(cubePath, typeof(Transform), "m_LocalScale.x"),
                    AnimationCurve.Linear(0, cubeScale.x, 4, cubeScale.x * 2));
                var endRotation = Quaternion.Euler(0, 0, 90);
                var rotationProperties = new[] { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
                for (int i = 0; i < 4; i++)
                {
                    AnimationUtility.SetEditorCurve(animation, EditorCurveBinding.FloatCurve(cubePath, typeof(Transform), rotationProperties[i]),
                        AnimationCurve.Linear(0, cubeRotation[i], 4, endRotation[i]));
                }
                var serialized = new SerializedObject(source);
                serialized.FindProperty("disassemblyClip").objectReferenceValue = animation;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                using (var explosion = new ModelExplodedView(source))
                {
                    Check(explosion.IsAvailable, "애니메이션 클립을 수동 재생 그래프로 연결합니다.");
                    explosion.Apply(0.25f);
                    Check(Mathf.Abs(cube.transform.localPosition.y - (cubeStart.y + 0.5f)) < 0.001f &&
                        Mathf.Abs(sphere.transform.localPosition.x - sphereStart.x) < 0.001f,
                        "진행도는 클립 시간으로 매핑하며 부품별 분해 순서를 유지합니다.");
                    explosion.Apply(1);
                    Check(Mathf.Abs(cube.transform.localPosition.y - (cubeStart.y + 2)) < 0.001f &&
                        Mathf.Abs(sphere.transform.localPosition.x - (sphereStart.x + 1)) < 0.001f,
                        "100퍼센트에서도 반복 없이 마지막 분해 포즈를 유지합니다.");
                    Check(Quaternion.Angle(cube.transform.localRotation, endRotation) < 0.1f &&
                        Mathf.Abs(cube.transform.localScale.x - cubeScale.x * 2) < 0.001f,
                        "위치뿐 아니라 클립의 회전과 크기도 재생합니다.");
                    explosion.Apply(0.25f);
                    Check(Mathf.Abs(cube.transform.localPosition.y - (cubeStart.y + 0.5f)) < 0.001f,
                        "역방향으로 이동해도 같은 키프레임 위치를 복원합니다.");
                    root.transform.rotation = Quaternion.Euler(15, 70, 20);
                    var rootRotation = root.transform.rotation;
                    explosion.Apply(1);
                    Check(Quaternion.Angle(root.transform.rotation, rootRotation) < 0.001f,
                        "분해 애니메이션은 모델 전체 공유 회전을 덮어쓰지 않습니다.");
                    explosion.Apply(0);
                    Check(sphere.transform.localPosition == sphereStart && cube.transform.localPosition == cubeStart &&
                        cube.transform.localRotation == cubeRotation && cube.transform.localScale == cubeScale,
                        "0퍼센트에서 최초 위치·회전·크기로 정확히 복원합니다.");
                    explosion.Apply(1);
                }
                Check(sphere.transform.localPosition == sphereStart && cube.transform.localPosition == cubeStart &&
                    cube.transform.localRotation == cubeRotation && cube.transform.localScale == cubeScale,
                    "재생 그래프를 해제하면 최초 포즈로 복원합니다.");
                var forward = new ModelPlaybackState(0.2f, 1, 10, 5);
                var reverse = new ModelPlaybackState(0.8f, -1, 10, 5);
                var paused = new ModelPlaybackState(0.4f, 0, 10, 5);
                Check(Mathf.Abs(forward.Evaluate(12) - 0.6f) < 0.0001f, "분해 재생은 서버 경과 시간으로 진행도를 계산합니다.");
                Check(Mathf.Abs(reverse.Evaluate(12) - 0.4f) < 0.0001f, "재조립은 현재 진행도에서 역방향으로 진행합니다.");
                Check(paused.Evaluate(100) == 0.4f, "일시정지 후 시간이 지나도 진행도는 유지됩니다.");
                Check(forward.Evaluate(100) == 1 && reverse.Evaluate(100) == 0 &&
                    forward.GetDirection(100) == 0 && reverse.GetDirection(100) == 0, "양쪽 끝에서는 포즈를 고정하고 재생을 멈춥니다.");
                Check(forward.Evaluate(9) == 0.2f, "시각 보정으로 시작 시각보다 앞서도 진행도가 역행하지 않습니다.");
                Check(new ModelPlaybackState(0.3f, 1, 0, 0).Evaluate(100) == 0.3f, "길이가 없는 상태는 나누기 없이 현재 진행도를 유지합니다.");
                serialized.Update();
                var stepValues = serialized.FindProperty("steps");
                var times = new[] { 3f, 0f, 1f, 5f, 1f };
                stepValues.arraySize = times.Length;
                for (int i = 0; i < times.Length; i++)
                {
                    var item = stepValues.GetArrayElementAtIndex(i);
                    item.FindPropertyRelative("time").floatValue = times[i];
                    item.FindPropertyRelative("title").stringValue = "단계 " + i;
                    item.FindPropertyRelative("description").stringValue = "단계 설명";
                    var targets = item.FindPropertyRelative("targets");
                    targets.arraySize = 1;
                    targets.GetArrayElementAtIndex(0).objectReferenceValue = sphere.transform;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var ordered = source.GetOrderedSteps();
                Check(ordered.Count == 3 && ordered[0].Time == 0 && ordered[1].Time == 1 && ordered[2].Time == 3,
                    "단계는 시간순으로 정렬하고 범위 밖 시간과 중복 시간을 제외합니다.");
                Check(source.FindStepIndex(ordered, 0) == 0 && source.FindStepIndex(ordered, 0.25f) == 1 &&
                    source.FindStepIndex(ordered, 1) == 2 && source.FindStepIndex(ordered, float.NaN) == -1,
                    "정방향·역방향·슬라이더 이동 모두 같은 단계 경계를 사용합니다.");
                Check(source.FindStepIndex(new[] { ordered[2] }, 0) == -1, "첫 단계 전의 구간은 시작 전 상태로 구분합니다.");
                serialized.Update();
                var firstTargets = serialized.FindProperty("steps").GetArrayElementAtIndex(1).FindPropertyRelative("targets");
                firstTargets.arraySize = 2;
                firstTargets.GetArrayElementAtIndex(0).objectReferenceValue = sphere.transform;
                firstTargets.GetArrayElementAtIndex(1).objectReferenceValue = cube.transform;
                var parentTargets = serialized.FindProperty("steps").GetArrayElementAtIndex(2).FindPropertyRelative("targets");
                parentTargets.arraySize = 2;
                parentTargets.GetArrayElementAtIndex(0).objectReferenceValue = assembly;
                parentTargets.GetArrayElementAtIndex(1).objectReferenceValue = sphere.transform;
                serialized.FindProperty("steps").GetArrayElementAtIndex(0).FindPropertyRelative("targets").arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                ordered = source.GetOrderedSteps();
                var viewer = root.AddComponent<ModelViewer>();
                Check(viewer.TryInitialize(out _), "통합 선택 확인용 모델을 초기화합니다.");
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var viewerHighlighter = (ModelPartHighlighter)typeof(ModelViewer).GetField("partHighlighter", flags).GetValue(viewer);
                var selectedNodes = (System.Collections.Generic.List<ModelPartTree.Node>)typeof(ModelViewer).GetField("selectedNodes", flags).GetValue(viewer);
                var selectStep = typeof(ModelViewer).GetMethod("SelectStepTargets", flags);
                Check(viewerHighlighter.SelectedCount == 0 && selectedNodes.Count == 0, "시작할 때는 아무 부품도 선택하지 않습니다.");
                selectStep.Invoke(viewer, new object[] { ordered[0] });
                sphere.GetPropertyBlock(highlighted, 0);
                cube.GetPropertyBlock(restored, 0);
                Check(selectedNodes.Count == 2 && viewerHighlighter.SelectedCount == 2 &&
                    highlighted.GetColor(colorId) == orange && restored.GetColor(colorId) == orange,
                    "단계의 여러 대상은 같은 주황색 선택 집합으로 표시합니다.");
                typeof(ModelViewer).GetMethod("SetSurroundingsTransparency", flags).Invoke(viewer, new object[] { true, 0.2f });
                Check(sphere.sharedMaterial == material && cube.sharedMaterial == material,
                    "단계로 선택한 모든 대상도 투명도 제외 대상으로 처리합니다.");
                typeof(ModelViewer).GetMethod("SelectPart", flags).Invoke(viewer, new object[] { cube });
                sphere.GetPropertyBlock(highlighted, 0);
                Check(selectedNodes.Count == 1 && viewerHighlighter.SelectedCount == 1 && viewerHighlighter.IsSelected(cube) &&
                    !viewerHighlighter.IsSelected(sphere) && Mathf.Approximately(highlighted.GetColor(colorId).a, 0.2f),
                    "직접 선택은 이전 단계 선택 전체를 교체하고 나머지에 투명도를 적용합니다.");
                selectStep.Invoke(viewer, new object[] { ordered[1] });
                Check(selectedNodes.Count == 1 && selectedNodes[0].Transform == assembly && viewerHighlighter.SelectedCount == 2,
                    "단계에 부모와 자식을 함께 지정해도 부모 묶음 하나로 정리합니다.");
                selectStep.Invoke(viewer, new object[] { ordered[2] });
                Check(selectedNodes.Count == 0 && viewerHighlighter.SelectedCount == 0 && sphere.sharedMaterial == material,
                    "유효한 단계 대상이 없으면 이전 선택을 남기지 않습니다.");
                var refreshStep = typeof(ModelViewer).GetMethod("RefreshStepSelection", flags);
                var seekStep = typeof(ModelViewer).GetMethod("SelectStepAtAmount", flags);
                refreshStep.Invoke(viewer, new object[] { 0, false });
                Check(selectedNodes.Count == 0, "처음 정지된 단계는 자동으로 선택하지 않습니다.");
                refreshStep.Invoke(viewer, new object[] { 0, true });
                Check(selectedNodes.Count == 2, "재생을 시작하면 현재 단계의 대상을 선택합니다.");
                typeof(ModelViewer).GetField("displayedExplosion", flags).SetValue(viewer, 0.3f);
                refreshStep.Invoke(viewer, new object[] { 0, false });
                Check(selectedNodes.Count == 1 && selectedNodes[0].Transform == assembly, "정방향 단계 경계에서 다음 대상을 선택합니다.");
                typeof(ModelViewer).GetField("displayedExplosion", flags).SetValue(viewer, 0.1f);
                refreshStep.Invoke(viewer, new object[] { 1, false });
                Check(selectedNodes.Count == 2, "재조립 중 이전 단계로 돌아가면 해당 대상을 선택합니다.");
                typeof(ModelViewer).GetMethod("SelectPart", flags).Invoke(viewer, new object[] { cube });
                refreshStep.Invoke(viewer, new object[] { 0, false });
                Check(selectedNodes.Count == 1 && viewerHighlighter.IsSelected(cube), "같은 단계에서는 직접 선택을 매 프레임 덮어쓰지 않습니다.");
                seekStep.Invoke(viewer, new object[] { 0.15f });
                Check(selectedNodes.Count == 2, "같은 단계 안에서 슬라이더를 조절해도 단계 대상을 다시 선택합니다.");
                typeof(ModelViewer).GetMethod("ClearSelection", flags).Invoke(viewer, null);
                sphere.GetPropertyBlock(highlighted, 0);
                cube.GetPropertyBlock(restored, 0);
                Check(selectedNodes.Count == 0 && highlighted.isEmpty && restored.GetColor(colorId) == Color.red &&
                    Mathf.Approximately(restored.GetFloat("_SavedValue"), 0.37f), "선택 해제는 단계·직접 선택 모두 같은 복구 경로를 사용합니다.");
                Debug.Log("MODEL STEP FOLLOW SELECTION CHECKS PASSED (69)");
            }
            finally
            {
                highlighter?.Dispose();
                if (animation != null)
                {
                    UnityEngine.Object.DestroyImmediate(animation);
                }
                UnityEngine.Object.DestroyImmediate(root);
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Renderer Part(GameObject root, PrimitiveType type, Vector3 position, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
