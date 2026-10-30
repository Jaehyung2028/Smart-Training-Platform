using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining.Editor
{
    public static class CommonModuleChecks
    {
        [MenuItem("Smart Training/Content/Check Common Modules")]
        public static void Run()
        {
            var preferences = new ClientPreferences();
            preferences.BeginEdit();
            preferences.Volume = 0.2f;
            preferences.FieldOfView = 95;
            preferences.InvertY = true;
            preferences.CancelEdit();
            Check(
                preferences.Volume == 1 && preferences.FieldOfView == 75 && !preferences.InvertY,
                "개인 설정 편집 취소"
            );
            preferences.Sensitivity = 2;
            preferences.BeginEdit();
            preferences.Reset();
            preferences.CancelEdit();
            Check(preferences.Sensitivity == 2, "기본값 복원도 취소 가능");
            var parent = new GameObject("Independent Settings Check");
            try
            {
                int changes = 0, saves = 0, cancels = 0;
                preferences.BeginEdit();
                UiPrefab.Instantiate("ClientSettingsPanel", parent.transform);
                var panel = new ClientSettingsPanel(parent.transform, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), preferences, () => changes++, () => saves++, () =>
                {
                    preferences.CancelEdit();
                    cancels++;
                });
                panel.Root.SetActive(true);
                var sliders = panel.Root.GetComponentsInChildren<Slider>();
                Check(sliders.Length == 3 && changes == 0, "설정 패널 생성은 값을 변경하지 않음");
                sliders[2].value = 0.3f;
                Check(Mathf.Abs(preferences.Volume - 0.3f) < 0.001f && changes == 1, "설정 패널 변경 콜백");
                panel.Refresh();
                Check(changes == 1, "설정 화면 갱신은 변경 이벤트를 재호출하지 않음");
                var buttons = panel.Root.GetComponentsInChildren<Button>();
                buttons.First(button => button.GetComponentInChildren<Text>().text == "취소하고 돌아가기").onClick.Invoke();
                Check(cancels == 1 && preferences.Volume == 1, "설정 패널 취소 콜백과 값 복구");
                buttons.First(button => button.GetComponentInChildren<Text>().text == "저장하고 돌아가기").onClick.Invoke();
                Check(saves == 1, "저장 시점을 호출자가 결정");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }

            using (var surface = new VideoSurfacePlayer(null, null, null, null))
            {
                Check(
                    !surface.TryInitialize(out var error) && !string.IsNullOrEmpty(error),
                    "영상 모듈의 누락 리소스 검사"
                );
                surface.SetVolume(0.4f);
                surface.Update(0, 0, false);
                Check(!surface.IsReady && surface.Volume == 0.4f, "미준비 영상 갱신·음량 설정의 안전성");
            }

            Debug.Log("COMMON MODULE CHECKS PASSED (9)");
        }


        private static void Check(bool condition, string name)
        {
            if (!condition)
            {
                throw new InvalidOperationException(name);
            }
        }
    }
}
