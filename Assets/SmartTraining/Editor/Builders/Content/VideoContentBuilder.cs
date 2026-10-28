using System;
using System.IO;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.Video;

namespace SmartTraining.Editor
{
    // 외부 영상 없이 실행할 수 있는 예시입니다. 한 번 만든 영상·프리팹은 덮어쓰지 않습니다.
    public static class VideoContentBuilder
    {
        public const string PrefabPath = "Assets/SmartTraining/Content/Videos/SafetyVideo.prefab";
        public const string AssetAddress = "content/video/safety-intro";
        private const string MoviePath = "Assets/SmartTraining/Content/Videos/SafetyIntro.mp4";

        [MenuItem("Smart Training/Content/Prepare Video Example")]
        public static void Prepare()
        {
            PrepareNetworkPrefab();
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            if (!File.Exists(MoviePath))
            {
                CreateMovie(MoviePath, "SAFETY TRAINING", new[] { "1 CHECK WORK AREA", "2 WEAR SAFETY GEAR", "3 FOLLOW SAFE STEPS" });
            }
            AssetDatabase.Refresh();
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(MoviePath);
            if (clip == null)
            {
                throw new InvalidOperationException("예시 영상 파일을 불러오지 못했습니다.");
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                CreateContentPrefab(clip);
            }
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(PrefabPath), settings.DefaultGroup);
            entry.address = AssetAddress;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("VIDEO CONTENT READY: " + AssetAddress);
        }

        public static void PrepareNetworkPrefab()
        {
            const string path = "Assets/SmartTraining/Resources/SmartTraining/SharedVideo.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                return;
            }
            var root = new GameObject("Shared Video", typeof(NetworkObject), typeof(SharedVideo));
            try
            {
                root.GetComponent<NetworkObject>().DontDestroyWithOwner = true;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateContentPrefab(VideoClip clip)
        {
            string materialPath = "Assets/SmartTraining/Content/Videos/VideoScreen.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Video Screen" };
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Cull", 0);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var root = new GameObject("Safety Video", typeof(VideoContentPlayer));
            try
            {
                var display = GameObject.CreatePrimitive(PrimitiveType.Quad);
                display.name = "Video Screen";
                display.transform.SetParent(root.transform, false);
                display.transform.localPosition = new Vector3(0, 2.1f, 4);
                display.transform.localScale = new Vector3(4.8f, 2.7f, 1);
                display.GetComponent<Renderer>().sharedMaterial = material;
                UnityEngine.Object.DestroyImmediate(display.GetComponent<Collider>());
                var serialized = new SerializedObject(root.GetComponent<VideoContentPlayer>());
                serialized.FindProperty("clip").objectReferenceValue = clip;
                serialized.FindProperty("subtitleFile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Assets/SmartTraining/Content/Videos/SafetyIntro.srt.txt");
                serialized.FindProperty("display").objectReferenceValue = display.GetComponent<Renderer>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.GetComponent<VideoPlayer>().playOnAwake = false;
                root.GetComponent<AudioSource>().playOnAwake = false;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // Unity 자체 인코더로 12초짜리 안내 영상을 만듭니다. 짧은 안내음도 포함해 음량 조절을 확인할 수 있습니다.
        public static void CreateMovie(string moviePath, string title, string[] headings)
        {
            const int width = 640, height = 360, fps = 15, seconds = 12, sampleRate = 48000;
            string encodedPath = Path.Combine(Path.GetTempPath(), "SmartTrainingVideo-" + Guid.NewGuid().ToString("N") + ".mp4");
            var frame = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            var video = new VideoTrackAttributes { width = width, height = height, frameRate = new MediaRational(fps), includeAlpha = false };
            var audio = new AudioTrackAttributes { channelCount = 2, sampleRate = new MediaRational(sampleRate), language = "" };
            try
            {
                using (var encoder = new MediaEncoder(encodedPath, video, audio))
                {
                    var samples = new NativeArray<float>(sampleRate / fps * 2, Allocator.Persistent);
                    try
                    {
                        for (int index = 0; index < fps * seconds; index++)
                        {
                            // 영상과 오디오를 같은 시간 구간씩 전달하여 인코더 대기열이 한쪽으로 쌓이지 않도록 합니다.
                            for (int sample = 0; sample < sampleRate / fps; sample++)
                            {
                                double time = (index * (sampleRate / fps) + sample) / (double)sampleRate;
                                double phase = time % 4;
                                float value = phase < 0.18 ? (float)(Math.Sin(time * Math.PI * 2 * 440) * 0.08 * (1 - phase / 0.18)) : 0;
                                samples[sample * 2] = samples[sample * 2 + 1] = value;
                            }
                            if (!encoder.AddSamples(samples))
                            {
                                throw new InvalidOperationException("예시 영상의 안내음을 인코딩하지 못했습니다.");
                            }
                            int step = index / (fps * 4);
                            Array.Fill(pixels, new Color32(15, 23, 42, 255));
                            Fill(pixels, width, height, 32, 40, 576, 6, new Color32(51, 65, 85, 255));
                            Fill(pixels, width, height, 32, 40, Mathf.RoundToInt(576f * index / (fps * seconds - 1)), 6, new Color32(96, 165, 250, 255));
                            DrawText(pixels, width, height, title, 32, 300, 4, Color.white);
                            DrawText(pixels, width, height, headings[step], 32, 246, 3, new Color32(74, 222, 128, 255));
                            DrawText(pixels, width, height, "TRAINING SAMPLE", 32, 72, 2, new Color32(203, 213, 225, 255));
                            // 이동하는 표시로 재생·정지·구간 이동을 명확하게 구분할 수 있습니다.
                            int travel = index % (fps * 4);
                            Fill(pixels, width, height, 32 + travel * 8, 135, 60, 60, new Color32(96, 165, 250, 255));
                            DrawText(pixels, width, height, (step + 1).ToString(), 542, 134, 8, Color.white);
                            frame.SetPixels32(pixels);
                            frame.Apply(false);
                            if (!encoder.AddFrame(frame))
                            {
                                throw new InvalidOperationException("예시 영상 프레임을 인코딩하지 못했습니다.");
                            }
                        }
                    }
                    finally
                    {
                        samples.Dispose();
                    }
                }
                File.Move(encodedPath, moviePath);
            }
            finally
            {
                if (File.Exists(encodedPath))
                {
                    File.Delete(encodedPath);
                }
                UnityEngine.Object.DestroyImmediate(frame);
            }
        }

        private static void Fill(Color32[] pixels, int width, int height, int x, int y, int w, int h, Color32 color)
        {
            for (int row = Math.Max(0, y); row < Math.Min(height, y + h); row++)
            {
                for (int column = Math.Max(0, x); column < Math.Min(width, x + w); column++)
                {
                    pixels[row * width + column] = color;
                }
            }
        }

        // 외부 폰트나 이미지 에셋을 요구하지 않는 작은 비트맵 글꼴입니다.
        private static void DrawText(Color32[] pixels, int width, int height, string text, int x, int y, int scale, Color32 color)
        {
            const string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ123";
            var glyphs = new[]
            {
                "01110/10001/10001/11111/10001/10001/10001", "11110/10001/10001/11110/10001/10001/11110",
                "01111/10000/10000/10000/10000/10000/01111", "11110/10001/10001/10001/10001/10001/11110",
                "11111/10000/10000/11110/10000/10000/11111", "11111/10000/10000/11110/10000/10000/10000",
                "01111/10000/10000/10111/10001/10001/01110", "10001/10001/10001/11111/10001/10001/10001",
                "11111/00100/00100/00100/00100/00100/11111", "00111/00010/00010/00010/10010/10010/01100",
                "10001/10010/10100/11000/10100/10010/10001", "10000/10000/10000/10000/10000/10000/11111",
                "10001/11011/10101/10101/10001/10001/10001", "10001/11001/10101/10011/10001/10001/10001",
                "01110/10001/10001/10001/10001/10001/01110", "11110/10001/10001/11110/10000/10000/10000",
                "01110/10001/10001/10001/10101/10010/01101", "11110/10001/10001/11110/10100/10010/10001",
                "01111/10000/10000/01110/00001/00001/11110", "11111/00100/00100/00100/00100/00100/00100",
                "10001/10001/10001/10001/10001/10001/01110", "10001/10001/10001/10001/10001/01010/00100",
                "10001/10001/10001/10101/10101/11011/10001", "10001/10001/01010/00100/01010/10001/10001",
                "10001/10001/01010/00100/00100/00100/00100", "11111/00001/00010/00100/01000/10000/11111",
                "00100/01100/00100/00100/00100/00100/01110", "01110/10001/00001/00010/00100/01000/11111",
                "11110/00001/00001/01110/00001/00001/11110"
            };
            foreach (char character in text)
            {
                int glyph = characters.IndexOf(character);
                if (glyph >= 0)
                {
                    var rows = glyphs[glyph].Split('/');
                    for (int row = 0; row < 7; row++)
                    {
                        for (int column = 0; column < 5; column++)
                        {
                            if (rows[row][column] == '1')
                            {
                                Fill(pixels, width, height, x + column * scale, y + (6 - row) * scale, scale, scale, color);
                            }
                        }
                    }
                }
                x += scale * 6;
            }
        }
    }
}
