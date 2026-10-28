using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.Video;

namespace SmartTraining.Editor
{
    public static class CourseSampleChecks
    {
        [MenuItem("Smart Training/Content/Check All Course Samples")]
        public static void Run()
        {
            SampleCourseBuilder.Prepare();
            // 서버 카탈로그가 원본입니다. 다른 폴더 배치에서는 환경변수로 검사할 경로를 지정합니다.
            string path = Environment.GetEnvironmentVariable("SMART_TRAINING_CATALOG") ??
                Path.GetFullPath("../Smart Training Server/Smart Training Server/Smart Training Server/Contents/catalog.json");
            var catalog = JArray.Parse(File.ReadAllText(path));
            Require(catalog.Count == 3, "교육과정 3개");
            var leaves = catalog.Descendants().OfType<JObject>()
                .Where(item => item["children"] is JArray children && children.Count == 0).ToArray();
            Require(leaves.Length == 9, "실행 콘텐츠 9개");
            Require(leaves.Select(item => (string)item["assetId"]).Distinct().Count() == 9, "샘플마다 독립된 주소");
            foreach (var item in leaves)
            {
                string address = (string)item["assetId"];
                var entry = AddressableAssetSettingsDefaultObject.Settings.groups
                    .Where(group => group != null).SelectMany(group => group.entries)
                    .SingleOrDefault(candidate => candidate.address == address);
                Require(entry != null, "주소 등록: " + address);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.AssetPath);
                Require(prefab != null, "프리팹 존재: " + address);
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    switch ((string)item["contentType"])
                    {
                        case "model":
                            Require(instance.GetComponent<ModelViewer>().TryInitialize(out _), "모델 메시: " + address);
                            Require(instance.GetComponentsInChildren<ModelPartInfo>().Length > 0, "부품 설명: " + address);
                            break;
                        case "video":
                            var video = new SerializedObject(instance.GetComponent<VideoContentPlayer>());
                            Require(video.FindProperty("clip").objectReferenceValue is VideoClip clip && clip.length > 0,
                                "영상 클립: " + address);
                            Require(video.FindProperty("subtitleFile").objectReferenceValue is TextAsset subtitles &&
                                subtitles.text.Contains("-->"), "자막: " + address);
                            break;
                        case "scenario":
                            var scenario = instance.GetComponent<ScenarioContent>();
                            Require(scenario.TryInitialize(out _), "시나리오 대상: " + address);
                            var data = new SerializedObject(scenario);
                            var definition = (ScenarioDefinition)data.FindProperty("definition").objectReferenceValue;
                            var progress = new ScenarioProgress(definition);
                            foreach (var step in definition.Steps)
                            {
                                if (step.RequiresTool)
                                {
                                    Require(progress.TrySelectTool(1, step.ToolId, progress.RunRevision, out _), "공구 선택");
                                }
                                Require(progress.TryInteract(1, step.TargetId, progress.Revision, out _), "단계 완료: " + step.Title);
                            }
                            Require(progress.IsComplete, "시나리오 전체 완료: " + address);
                            break;
                        default:
                            throw new InvalidOperationException("실행 불가능한 콘텐츠 유형: " + address);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
            Debug.Log("ALL COURSE SAMPLE CHECKS PASSED (3 courses / 9 contents)");
        }

        private static void Require(bool success, string message)
        {
            if (!success)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
