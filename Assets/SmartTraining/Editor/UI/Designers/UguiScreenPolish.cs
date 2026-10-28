using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining.Editor
{
    // 사용자가 명시적으로 실행하는 디자인 정리입니다. 기존 오브젝트·입력·버튼 연결을 재생성하지 않습니다.
    public static class UguiScreenPolish
    {
        private const string SpritePath = "Assets/SmartTraining/Resources/SmartTraining/UI/RoundedSurface.png";

        [MenuItem("Smart Training/UI/Polish All Screens")]
        public static void ApplyAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            PrepareSurface();
            var theme = UguiLoginDesigner.LoadTheme();
            theme.background = new Color32(233, 239, 247, 255);
            theme.surface = Color.white;
            theme.field = new Color32(248, 250, 253, 255);
            theme.ink = new Color32(24, 39, 60, 255);
            theme.muted = new Color32(102, 119, 141, 255);
            theme.line = new Color32(216, 226, 238, 255);
            theme.secondary = new Color32(237, 242, 249, 255);
            EditorUtility.SetDirty(theme);
            foreach (string path in new[]
            {
                LobbyUguiBuilder.AssetPath,
                LobbyInteriorUguiBuilder.InteriorPath,
                RoomUguiBuilder.AssetPath
            }

            )
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.TryGetComponent<LobbyUguiView>(out _))
                    {
                        UguiLoginDesigner.Apply(root, theme);
                        UguiCatalogDesigner.Apply(root, theme);
                    }
                    else if (root.TryGetComponent<LobbyInteriorUguiView>(out _))
                    {
                        UguiInteriorDesigner.Apply(root, theme);
                    }
                    else
                    {
                        UguiRoomDesigner.Apply(root, theme);
                    }

                    Polish(root, theme);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            // 씬에 연결된 인스턴스는 프리팹 변경을 상속하며 개별 수정은 유지합니다.
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path == UguiSceneDesigner.ClientScene)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            Debug.Log("UI VISUAL POLISH APPLIED: login, catalog, interior, room");
        }


        private static void PrepareSurface()
        {
            if (!File.Exists(SpritePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));
                const int size = 64;
                const float radius = 12;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                try
                {
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = Mathf.Max(radius - (x + .5f), x + .5f - (size - radius), 0);
                            float dy = Mathf.Max(radius - (y + .5f), y + .5f - (size - radius), 0);
                            float alpha = Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy));
                            texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                        }
                    }

                    texture.Apply();
                    File.WriteAllBytes(SpritePath, texture.EncodeToPNG());
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }

                AssetDatabase.ImportAsset(SpritePath);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(14, 14, 14, 14);
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }


        private static void Polish(GameObject root, UguiTheme theme)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                UguiVisualStyle.Round(button.GetComponent<Image>());
                var colors = button.colors;
                colors.fadeDuration = .14f;
                colors.disabledColor = new Color32(202, 213, 229, 255);
                button.colors = colors;
                var label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.lineSpacing = 1.1f;
                }
            }

            foreach (var input in root.GetComponentsInChildren<InputField>(true))
            {
                UguiVisualStyle.Round(input.GetComponent<Image>());
                var textRect = input.textComponent.rectTransform;
                textRect.offsetMin = new Vector2(14, textRect.offsetMin.y);
                textRect.offsetMax = new Vector2(-14, textRect.offsetMax.y);
            }

            foreach (var scroll in root.GetComponentsInChildren<ScrollRect>(true))
            {
                UguiVisualStyle.Round(scroll.GetComponent<Image>());
            }

            var main = root.transform.Find("Background/Main Panel");
            if (main != null)
            {
                UguiVisualStyle.Round(main.GetComponent<Image>());
                var layout = main.GetComponent<VerticalLayoutGroup>();
                if (layout != null)
                {
                    layout.padding = new RectOffset(24, 24, 20, 20);
                    layout.spacing = 8;
                }
            }

            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Card" || image.name == "Login Form")
                {
                    UguiVisualStyle.Round(image);
                }
            }

            if (root.TryGetComponent<RoomUguiView>(out var room))
            {
                PolishRoom(room, theme);
            }

            if (root.TryGetComponent<LobbyInteriorUguiView>(out var interior))
            {
                interior.heading.fontSize = 27;
                interior.roomCount.fontSize = interior.participantTitle.fontSize = 17;
                UguiVisualStyle.Round(interior.roomTemplate.GetComponent<Image>());
                UguiVisualStyle.Round(interior.participantTemplate.GetComponent<Image>());
            }

            if (root.TryGetComponent<LobbyUguiView>(out var login))
            {
                foreach (var dropdown in root.GetComponentsInChildren<Dropdown>(true))
                {
                    UguiVisualStyle.Round(dropdown.GetComponent<Image>());
                    if (dropdown.captionText != null)
                    {
                        dropdown.captionText.fontSize = 16;
                    }
                }
            }
        }


        private static void PolishRoom(RoomUguiView room, UguiTheme theme)
        {
            room.heading.fontSize = 28;
            Height(room.heading.gameObject, 36);
            Height(room.summary.gameObject, 40);
            Height(room.connection.gameObject, 28);
            Height(room.status.gameObject, 26);
            var noticeScroll = room.notices.GetComponentInParent<ScrollRect>();
            if (noticeScroll != null) { Height(noticeScroll.gameObject, 44); }
            var noticeHeader = room.clearNotices.transform.parent;
            if (noticeHeader.GetComponent<LayoutElement>() != null) { Height(noticeHeader.gameObject, 26); }
            room.summary.fontSize = 16;
            room.connection.fontSize = 13;
            room.participantTitle.fontSize = 17;
            var actions = room.phase.transform.parent.GetComponent<HorizontalLayoutGroup>();
            actions.childForceExpandWidth = false;
            foreach (var button in new[]
            {
                room.ready,
                room.phase
            }

            )
            {
                Width(button, 220);
            }

            Width(room.settings, 160);
            var footer = room.leave.transform.parent.GetComponent<HorizontalLayoutGroup>();
            footer.childForceExpandWidth = false;
            footer.childAlignment = TextAnchor.MiddleRight;
            Width(room.leave, 168);
            Width(room.logout, 128);
            var card = room.participantTemplate.GetComponent<RoomParticipantCard>();
            UguiVisualStyle.Round(room.participantTemplate.GetComponent<Image>());
            card.details.fontSize = 17;
            card.details.GetComponent<LayoutElement>().preferredHeight = 42;
            card.details.GetComponent<LayoutElement>().minHeight = 42;
            if (card.learning == null)
            {
                var badge = new GameObject("Learning Result", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
                badge.transform.SetParent(room.participantTemplate, false);
                badge.transform.SetSiblingIndex(card.details.transform.GetSiblingIndex() + 1);
                card.learning = badge.GetComponent<Text>();
                card.learning.font = card.details.font;
                card.learning.fontSize = 14;
                card.learning.fontStyle = FontStyle.Bold;
                card.learning.raycastTarget = false;
                card.learning.text = "학습 미시작";
                badge.GetComponent<LayoutElement>().preferredHeight = 24;
            }
        }


        private static void Height(GameObject target, float height)
        {
            var size = target.GetComponent<LayoutElement>();
            if (size != null) { size.minHeight = size.preferredHeight = height; }
        }

        private static void Width(Button button, float width)
        {
            var size = button.GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = width;
            size.flexibleWidth = 0;
        }
    }
}
