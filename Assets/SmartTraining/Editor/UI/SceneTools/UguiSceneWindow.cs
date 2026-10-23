using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SmartTraining.Editor
{
    public sealed class UguiSceneWindow : EditorWindow
    {
        private UguiSceneLayout layout;
        private readonly List<string> errors = new();
        private string message = "";
        private Vector2 scroll;
        private static readonly string[] labels = { "로그인", "콘텐츠 로비 목록", "로비 입장 중", "로비 내부", "방 생성 창", "방 입장 중", "방 내부", "방장 설정", "방 관리 확인", "위치·이동 창" };
        private void OnEnable()
        {
            minSize = new Vector2(340, 400);
            Selection.selectionChanged += Repaint;
        }
        private void OnDisable()
        {
            Selection.selectionChanged -= Repaint;
            UguiScenePreview.Restore();
        }

        private void OnGUI()
        {
            if (layout == null)
            {
                var layouts = UguiSceneDesigner.InScene<UguiSceneLayout>(SceneManager.GetActiveScene());
                if (layouts.Length == 1)
                    layout = layouts[0];
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("씬 UI 편집", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("아래 화면을 선택한 뒤 Game 탭에서 배치를 확인하세요. 서버에 접속하지 않습니다. 위치·색상·크기는 Hierarchy와 Inspector에서 수정할 수 있습니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("현재 씬에 UI 배치·연결"))
                    Run(() => { layout = UguiSceneDesigner.Install(SceneManager.GetActiveScene()); Check(); message = "배치했습니다. 씬 저장은 직접 진행해주세요."; });
                var selected = (UguiSceneLayout)EditorGUILayout.ObjectField("UI Workspace", layout, typeof(UguiSceneLayout), true);
                if (selected != layout)
                {
                    UguiScenePreview.Restore();
                    layout = selected;
                    errors.Clear();
                }
                if (GUILayout.Button("버튼·입력창 연결 검사"))
                    Run(Check);
                using (new EditorGUI.DisabledScope(layout == null))
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("화면 미리보기", EditorStyles.boldLabel);
                    for (int i = 0; i < labels.Length; i++)
                    {
                        var mode = (UguiPreviewScreen)i;
                        string prefix = UguiScenePreview.IsActive && UguiScenePreview.Layout == layout && UguiScenePreview.Current == mode ? "● " : "";
                        if (GUILayout.Button(prefix + labels[i]))
                            Run(() => { UguiScenePreview.Show(layout, mode); message = labels[(int)mode] + " 미리보기 중"; });
                    }
                    if (GUILayout.Button("미리보기 종료·원래 표시 복구"))
                    {
                        UguiScenePreview.Restore();
                        message = "원래 표시로 복구했습니다.";
                    }
                    if (GUILayout.Button("Hierarchy에서 Workspace 선택"))
                        Selection.activeGameObject = layout.gameObject;
                }
            }
            EditorGUILayout.Space();
            if (message.Length > 0)
                EditorGUILayout.HelpBox(message, MessageType.Info);
            foreach (var error in errors)
                EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.HelpBox("미리보기는 씬 저장·플레이·빌드 전에 자동으로 종료됩니다. 창을 닫아도 원래 표시로 돌아옵니다. 실제 상태 동기화와 버튼 동작은 플레이에서 확인해주세요.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }
        private void Check()
        {
            errors.Clear();
            errors.AddRange(UguiSceneDesigner.Validate(layout));
            message = errors.Count == 0 ? "연결 검사 완료: 누락이나 중복이 없습니다." : "연결 오류 " + errors.Count + "개를 확인해주세요.";
        }
        private void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception error) { errors.Clear(); errors.Add(error.Message); message = ""; }
        }
    }

    [CustomEditor(typeof(UguiSceneLayout))]
    public sealed class UguiSceneLayoutInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("씬 UI 편집 창 열기"))
                UguiSceneDesigner.OpenWindow();
            var errors = UguiSceneDesigner.Validate((UguiSceneLayout)target);
            if (errors.Count == 0)
                EditorGUILayout.HelpBox("필수 UI 연결이 모두 정상입니다.", MessageType.Info);
            else
            foreach (var error in errors)
                EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }
}
