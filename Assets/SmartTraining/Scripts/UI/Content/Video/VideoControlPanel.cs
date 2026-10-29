using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SmartTraining
{
    // 공유 재생 조작과 개인 음량을 분리합니다. 창을 열어도 캐릭터 이동과 시점을 멈추지 않습니다.
    public sealed class VideoControlPanel : IDisposable
    {
        private readonly GameObject root;
        private readonly Font font;
        private readonly Text status, time, volumeLabel;
        private readonly Button play, backward, forward, stop, subtitleToggle;
        private readonly Text subtitleLabel, subtitleToggleLabel;
        private readonly GameObject subtitleRoot;
        private readonly Text playLabel;
        private readonly Slider seek, volume;
        private VideoContentPlayer viewer;
        private bool pending, dragging;
        private float requested, nextSend, editedAt = float.NegativeInfinity;

        public VideoControlPanel(Transform parent, Transform subtitleParent, Font font)
        {
            this.font = font;
            root = new GameObject(
                "Video Controls",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter)
            );
            root.transform.SetParent(parent, false);
            root.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            status = Label("Video Status", 16, 28);
            status.resizeTextForBestFit = true;
            status.resizeTextMinSize = 12;
            status.resizeTextMaxSize = 16;
            time = Label("Video Time", 16, 22);
            seek = Slider("Video Seek");
            var trigger = seek.gameObject.AddComponent<EventTrigger>();
            var down = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerDown
            };
            down.callback.AddListener(_ => dragging = true);
            var up = new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerUp
            };
            up.callback.AddListener(_ => dragging = false);
            trigger.triggers.Add(down);
            trigger.triggers.Add(up);
            seek.onValueChanged.AddListener(value =>
            {
                if (seek.IsInteractable())
                {
                    requested = value;
                    editedAt = Time.unscaledTime;
                    pending = true;
                }
            });
            var controls = new GameObject(
                "Video Playback Buttons",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement)
            );
            controls.transform.SetParent(root.transform, false);
            controls.GetComponent<LayoutElement>().preferredHeight = 34;
            var buttonsLayout = controls.GetComponent<HorizontalLayoutGroup>();
            buttonsLayout.spacing = 4;
            buttonsLayout.childControlWidth = buttonsLayout.childControlHeight = true;
            buttonsLayout.childForceExpandWidth = true;
            buttonsLayout.childForceExpandHeight = false;
            backward = CreatePlaybackButton("Video Backward", "5초 뒤로", controls.transform, () => viewer.Skip(-5));
            play = CreatePlaybackButton("Video Play Pause", "재생", controls.transform, () => viewer.TogglePlayback());
            playLabel = play.GetComponentInChildren<Text>();
            forward = CreatePlaybackButton("Video Forward", "5초 앞으로", controls.transform, () => viewer.Skip(5));
            var options = new GameObject(
                "Video Options Buttons",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement)
            );
            options.transform.SetParent(root.transform, false);
            options.GetComponent<LayoutElement>().preferredHeight = 34;
            var optionsLayout = options.GetComponent<HorizontalLayoutGroup>();
            optionsLayout.spacing = 4;
            optionsLayout.childControlWidth = optionsLayout.childControlHeight = true;
            optionsLayout.childForceExpandWidth = true;
            optionsLayout.childForceExpandHeight = false;
            stop = CreatePlaybackButton("Video Stop", "정지", options.transform, () => viewer.StopPlayback());
            subtitleToggle = CreatePlaybackButton(
                "Video Subtitle Toggle",
                "자막 켜짐",
                options.transform,
                () => viewer.ToggleSubtitles(),
                flushPendingSeek: false
            );
            subtitleToggleLabel = subtitleToggle.GetComponentInChildren<Text>();
            subtitleRoot = new GameObject("Video Subtitles", typeof(RectTransform), typeof(Image));
            subtitleRoot.transform.SetParent(subtitleParent, false);
            var subtitleRect = subtitleRoot.GetComponent<RectTransform>();
            subtitleRect.anchorMin = subtitleRect.anchorMax = subtitleRect.pivot = new Vector2(0.5f, 0);
            subtitleRect.anchoredPosition = new Vector2(0, 100);
            subtitleRect.sizeDelta = new Vector2(900, 92);
            subtitleRoot.GetComponent<Image>().color = new Color(0, 0, 0, 0.75f);
            subtitleRoot.GetComponent<Image>().raycastTarget = false;
            subtitleLabel = Text("Video Subtitle Text", subtitleRoot.transform, 24);
            subtitleLabel.alignment = TextAnchor.MiddleCenter;
            subtitleLabel.resizeTextForBestFit = true;
            subtitleLabel.resizeTextMinSize = 16;
            subtitleLabel.resizeTextMaxSize = 24;
            subtitleLabel.rectTransform.anchorMin = Vector2.zero;
            subtitleLabel.rectTransform.anchorMax = Vector2.one;
            subtitleLabel.rectTransform.offsetMin = new Vector2(16, 8);
            subtitleLabel.rectTransform.offsetMax = new Vector2(-16, -8);
            subtitleRoot.SetActive(false);
            volumeLabel = Label("Video Volume Label", 16, 22);
            volume = Slider("Video Local Volume");
            volume.onValueChanged.AddListener(value => viewer?.SetVolume(value));
            root.SetActive(false);
        }


        internal void Refresh(VideoContentPlayer current, bool visible, bool canInteract)
        {
            if (viewer != current)
            {
                pending = dragging = false;
                editedAt = float.NegativeInfinity;
                viewer = current;
            }

            root.SetActive(visible && viewer != null);
            string caption = root.activeSelf ? viewer.SubtitleText : "";
            subtitleRoot.SetActive(!string.IsNullOrEmpty(caption));
            if (!root.activeSelf)
            {
                pending = dragging = false;
                return;
            }

            subtitleLabel.text = caption;
            var subtitleRect = subtitleRoot.GetComponent<RectTransform>();
            var canvasRect = (RectTransform)subtitleRoot.transform.parent;
            float subtitleBottom = 100;
            if (root.activeInHierarchy)
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, root.transform);
                subtitleBottom = bounds.max.y - canvasRect.rect.yMin + 12;
            }

            subtitleRect.anchoredPosition = new Vector2(0, Mathf.Min(subtitleBottom, canvasRect.rect.height - 170));
            subtitleRect.sizeDelta = new Vector2(Mathf.Min(900, canvasRect.rect.width - 48), 92);
            subtitleToggle.interactable = canInteract && viewer.HasSubtitles;
            subtitleToggleLabel.text = !viewer.HasSubtitles ? "자막 없음" : viewer.SubtitlesEnabled ? "자막 켜짐" : "자막 꺼짐";
            bool canControl = canInteract && viewer.CanControl;
            seek.interactable = play.interactable = backward.interactable = forward.interactable = stop.interactable = canControl;
            if (!canControl)
            {
                pending = dragging = false;
            }

            if (dragging && (Mouse.current == null || !Mouse.current.leftButton.isPressed))
            {
                dragging = false;
            }

            if (pending && Time.unscaledTime >= nextSend)
            {
                FlushSeek();
            }

            if (!pending && !dragging && Time.unscaledTime - editedAt > 0.3f)
            {
                seek.SetValueWithoutNotify(viewer.Duration > 0 ? (float)(viewer.Position / viewer.Duration) : 0);
            }

            status.text = viewer.Status + (viewer.CanControl ? " · 모든 참가자 공동 조작" : " · 영상 준비 후 조작할 수 있습니다.");
            status.color = viewer.Error.Length > 0 ? new Color32(248, 113, 113, 255) : Color.white;
            time.text = FormatTime(viewer.Position) + " / " + FormatTime(viewer.Duration);
            playLabel.text = viewer.IsPlaying ? "일시정지" : "재생";
            volume.interactable = canInteract;
            volume.SetValueWithoutNotify(viewer.Volume);
            volumeLabel.text = "내 영상 음량: " + Mathf.RoundToInt(viewer.Volume * 100) + "%";
        }


        public void Dispose()
        {
            root.SetActive(false);
            subtitleRoot.SetActive(false);
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(subtitleRoot);
        }


        private Button CreatePlaybackButton(
            string name,
            string caption,
            Transform parent,
            Action action,
            bool flushPendingSeek = true
        )
        {
            var button = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement)
            ).GetComponent<Button>();
            button.transform.SetParent(parent, false);
            button.GetComponent<LayoutElement>().preferredHeight = 34;
            button.GetComponent<Image>().color = new Color32(51, 65, 85, 255);
            button.targetGraphic = button.GetComponent<Image>();
            var label = Text("Label", button.transform, 12);
            label.text = caption;
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            button.onClick.AddListener(() =>
            {
                if (button.IsInteractable() && viewer != null)
                {
                    if (flushPendingSeek)
                    {
                        FlushSeek();
                    }

                    action();
                }
            });
            return button;
        }


        private void FlushSeek()
        {
            if (pending && viewer != null && viewer.CanControl)
            {
                viewer.Seek(requested);
                nextSend = Time.unscaledTime + 0.1f;
            }

            pending = false;
        }


        private static string FormatTime(double seconds)
        {
            var value = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return ((int)value.TotalMinutes).ToString("00") + ":" + value.Seconds.ToString("00");
        }


        private Text Label(string name, int size, float height)
        {
            var label = Text(name, root.transform, size);
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return label;
        }


        private Text Text(string name, Transform parent, int size)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }


        private Slider Slider(string name)
        {
            var slider = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Slider),
                typeof(LayoutElement)
            ).GetComponent<Slider>();
            slider.transform.SetParent(root.transform, false);
            slider.GetComponent<Image>().color = new Color32(71, 85, 105, 255);
            slider.GetComponent<LayoutElement>().preferredHeight = 24;
            var area = new GameObject("Handle Area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(slider.transform, false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(10, 0);
            area.offsetMax = new Vector2(-10, 0);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            handle.SetParent(area, false);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.up;
            handle.sizeDelta = new Vector2(20, 0);
            handle.GetComponent<Image>().color = new Color32(96, 165, 250, 255);
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.handleRect = handle;
            return slider;
        }
    }
}
