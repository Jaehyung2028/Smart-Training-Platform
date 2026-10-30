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
        private readonly Text status, time, volumeLabel;
        private readonly Button play, backward, forward, stop, subtitleToggle;
        private readonly Text subtitleLabel, subtitleToggleLabel;
        private readonly GameObject subtitleRoot;
        private readonly Vector2 subtitleSize;
        private readonly Vector2 subtitlePosition;
        private readonly Text playLabel;
        private readonly Slider seek, volume;
        private VideoContentPlayer viewer;
        private bool pending, dragging;
        private float requested, nextSend, editedAt = float.NegativeInfinity;

        public VideoControlPanel(Transform parent, Transform subtitleParent, Font font)
        {
            var view = UiPrefab.Instantiate("VideoControlPanel", parent);
            root = view.Get<GameObject>("root");
            status = view.Get<Text>("status");
            time = view.Get<Text>("time");
            volumeLabel = view.Get<Text>("volumeLabel");
            play = view.Get<Button>("play");
            backward = view.Get<Button>("backward");
            forward = view.Get<Button>("forward");
            stop = view.Get<Button>("stop");
            subtitleToggle = view.Get<Button>("subtitleToggle");
            subtitleToggleLabel = view.Get<Text>("subtitleToggleLabel");
            playLabel = view.Get<Text>("playLabel");
            seek = view.Get<Slider>("seek");
            volume = view.Get<Slider>("volume");
            var subtitles = UiPrefab.Instantiate("VideoSubtitles", subtitleParent);
            subtitleRoot = subtitles.gameObject;
            subtitleSize = ((RectTransform)subtitleRoot.transform).sizeDelta;
            subtitlePosition = ((RectTransform)subtitleRoot.transform).anchoredPosition;
            subtitleLabel = subtitles.Get<Text>("Video Subtitle Text:Text");
            var trigger = seek.GetComponent<EventTrigger>();
            trigger.triggers.Clear();
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
            BindPlayback(backward, () => viewer.Skip(-5));
            BindPlayback(play, () => viewer.TogglePlayback());
            BindPlayback(forward, () => viewer.Skip(5));
            BindPlayback(stop, () => viewer.StopPlayback());
            BindPlayback(subtitleToggle, () => viewer.ToggleSubtitles(), false);
            volume.onValueChanged.AddListener(value => viewer?.SetVolume(value));
            root.SetActive(false);
            subtitleRoot.SetActive(false);
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

            subtitleRect.anchoredPosition = new Vector2(
                subtitlePosition.x,
                Mathf.Min(
                Mathf.Max(subtitlePosition.y, subtitleBottom),
                canvasRect.rect.height - 170
            )
            );
            subtitleRect.sizeDelta = new Vector2(Mathf.Min(subtitleSize.x, canvasRect.rect.width - 48), subtitleSize.y);
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
            UiPrefab.Destroy(root);
            UiPrefab.Destroy(subtitleRoot);
        }


        private void BindPlayback(Button button, Action action, bool flushPendingSeek = true)
        {
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
    }
}
