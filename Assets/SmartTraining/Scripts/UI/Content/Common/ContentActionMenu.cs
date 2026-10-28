using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // 오른쪽 아래의 콘텐츠 기능 메뉴입니다. 콘텐츠마다 필요한 기능만 등록하고 실제 처리는 각 콘텐츠에 맡깁니다.
    public sealed class ContentActionMenu
    {
        private readonly GameObject root;
        private readonly GameObject panel;
        private readonly Font font;
        private readonly Button toggleButton;
        private readonly Text toggleLabel;
        private readonly Text emptyLabel;
        private readonly List<(Button Button, Func<bool> CanExecute)> actions = new();

        private readonly List<GameObject> extraControls = new();
        private readonly List<(Selectable Control, Func<bool> CanUse)> controlStates = new();

        private readonly List<Action<bool>> sharedSliders = new();
        private readonly List<Action> flushSliders = new();
        private readonly List<Action> cancelSliders = new();

        public bool IsOpen => panel != null && panel.activeSelf;

        public ContentActionMenu(Transform parent, Font font)
        {
            this.font = font;
            root = new GameObject("Content Actions", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.right;
            rect.anchoredPosition = new Vector2(-24, 24);
            rect.sizeDelta = new Vector2(240, 48);
            toggleButton = CreateButton("기능 메뉴", root.transform);
            toggleLabel = toggleButton.GetComponentInChildren<Text>();
            var toggleRect = toggleButton.GetComponent<RectTransform>();
            toggleRect.anchorMin = Vector2.zero;
            toggleRect.anchorMax = Vector2.one;
            toggleRect.offsetMin = toggleRect.offsetMax = Vector2.zero;
            toggleButton.onClick.AddListener(Toggle);

            panel = new GameObject("Content Action List", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(root.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.right;
            panelRect.pivot = Vector2.zero;
            panelRect.anchoredPosition = new Vector2(0, 60);
            panelRect.sizeDelta = Vector2.zero;
            panel.GetComponent<Image>().color = new Color32(15, 23, 42, 245);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            emptyLabel = CreateLabel("등록된 기능이 없습니다.", panel.transform);
            emptyLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            Close();
        }

        // 콘텐츠 전용 UI도 같은 기능 메뉴의 배치·닫기·정리 수명주기에 포함합니다.
        public RectTransform AddSection(string name)
        {
            var section = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            section.transform.SetParent(panel.transform, false);
            var layout = section.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            extraControls.Add(section);
            emptyLabel.gameObject.SetActive(false);
            return section.GetComponent<RectTransform>();
        }

        public void AddAction(string label, Action execute, Func<bool> canExecute)
        {
            var button = CreateButton(label, panel.transform);
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            button.onClick.AddListener(() =>
            {
                // 표시 직후 상태가 바뀔 수 있으므로 클릭할 때도 조건을 확인합니다. 서버는 다시 권한을 검사합니다.
                if (button.interactable && canExecute())
                {
                    execute();
                }
            });
            actions.Add((button, canExecute));
            emptyLabel.gameObject.SetActive(false);
        }

        // 슬라이더는 메뉴를 닫지 않습니다. 선택 변경 후에도 개인별 설정을 그대로 사용합니다.
        public void AddTransparencyControls(Action<bool, float> apply, Func<bool> canUse)
        {
            bool enabled = false;
            float transparency = 0.7f;
            var toggle = CreateButton("주변 반투명: 꺼짐", panel.transform);
            toggle.gameObject.AddComponent<LayoutElement>().preferredHeight = 44;
            var caption = CreateLabel("주변 부품 투명도: 70%", panel.transform);
            caption.fontSize = 16;
            caption.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
            var slider = CreateSlider("Surroundings Transparency");
            slider.SetValueWithoutNotify(transparency);
            toggle.onClick.AddListener(() =>
            {
                if (!canUse())
                {
                    return;
                }
                enabled = !enabled;
                toggle.GetComponentInChildren<Text>().text = "주변 반투명: " + (enabled ? "켜짐" : "꺼짐");
                apply(enabled, 1 - transparency);
                slider.interactable = enabled && canUse();
            });
            slider.onValueChanged.AddListener(value =>
            {
                transparency = value;
                caption.text = "주변 부품 투명도: " + Mathf.RoundToInt(value * 100) + "%";
                apply(enabled, 1 - value);
            });
            extraControls.Add(toggle.gameObject);
            extraControls.Add(caption.gameObject);
            extraControls.Add(slider.gameObject);
            controlStates.Add((toggle, canUse));
            controlStates.Add((slider, () => enabled && canUse()));
            emptyLabel.gameObject.SetActive(false);
        }

        private Slider CreateSlider(string name)
        {
            var slider = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Slider), typeof(LayoutElement)).GetComponent<Slider>();
            slider.transform.SetParent(panel.transform, false);
            slider.GetComponent<Image>().color = new Color32(71, 85, 105, 255);
            slider.GetComponent<LayoutElement>().preferredHeight = 28;
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

        // 서버의 공통 값을 표시합니다. 드래그 중 최신 입력만 0.1초 간격으로 보내고 마지막 값도 전송합니다.
        public void AddSharedSlider(string title, string name, Func<float> getValue, Action<float> setValue, Func<bool> canUse)
        {
            var caption = CreateLabel(title + ": 0%", panel.transform);
            caption.fontSize = 16;
            caption.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
            var slider = CreateSlider(name);
            slider.SetValueWithoutNotify(getValue());
            bool pending = false;
            float requested = slider.value;
            float nextSend = 0;
            float editedAt = float.NegativeInfinity;
            slider.onValueChanged.AddListener(value =>
            {
                if (!slider.IsInteractable())
                {
                    return;
                }
                requested = value;
                editedAt = Time.unscaledTime;
                pending = true;
            });
            Action flush = () =>
            {
                if (pending && canUse())
                {
                    setValue(requested);
                    nextSend = Time.unscaledTime + 0.1f;
                }
                pending = false;
            };
            flushSliders.Add(flush);
            cancelSliders.Add(() =>
            {
                pending = false;
                editedAt = float.NegativeInfinity;
            });
            sharedSliders.Add(canInteract =>
            {
                bool allowed = canInteract && canUse();
                slider.interactable = allowed;
                if (!allowed)
                {
                    pending = false;
                }
                if (pending && Time.unscaledTime >= nextSend)
                {
                    flush();
                }
                // 본인이 조절 중일 때 늦게 받은 이전 값으로 손잡이가 되돌아가지 않게 합니다.
                if (!pending && Time.unscaledTime - editedAt > 0.25f)
                {
                    slider.SetValueWithoutNotify(getValue());
                }
                caption.text = title + ": " + Mathf.RoundToInt(slider.value * 100) + "%";
            });
            extraControls.Add(caption.gameObject);
            extraControls.Add(slider.gameObject);
            emptyLabel.gameObject.SetActive(false);
        }

        public void AddPlaybackControls(Action<int> execute, Func<int, bool> canExecute, Func<string> getStatus)
        {
            var status = CreateLabel("조립 상태", panel.transform);
            status.fontSize = 16;
            status.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
            var row = new GameObject("Model Playback Controls", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(panel.transform, false);
            row.GetComponent<LayoutElement>().preferredHeight = 40;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 4;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            foreach (var item in new[] { (Label: "분해 재생", Direction: 1), (Label: "일시정지", Direction: 0), (Label: "재조립", Direction: -1) })
            {
                int direction = item.Direction;
                var button = CreateButton(item.Label, row.transform);
                button.GetComponentInChildren<Text>().fontSize = 13;
                button.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                button.onClick.AddListener(() =>
                {
                    if (button.IsInteractable() && canExecute(direction))
                    {
                        // 보류 중인 슬라이더 이동을 먼저 보낸 뒤 재생합니다. 늦은 이동 요청이 재생을 다시 멈추지 않게 합니다.
                        foreach (var flush in flushSliders)
                        {
                            flush();
                        }
                        execute(direction);
                    }
                });
                actions.Add((button, () => canExecute(direction)));
            }
            sharedSliders.Add(_ => status.text = getStatus());
            extraControls.Add(status.gameObject);
            extraControls.Add(row);
            emptyLabel.gameObject.SetActive(false);
        }

        // 단계 이동을 누른 뒤 이전 슬라이더 입력이 뒤늦게 도착해 이동을 덮어쓰지 않게 합니다.
        public void CancelPendingSliderChanges()
        {
            foreach (var cancel in cancelSliders)
            {
                cancel();
            }
        }

        public void ClearActions()
        {
            Close();
            foreach (var action in actions)
            {
                action.Button.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(action.Button.gameObject);
            }
            actions.Clear();
            foreach (var control in extraControls)
            {
                control.SetActive(false);
                UnityEngine.Object.Destroy(control);
            }
            extraControls.Clear();
            controlStates.Clear();
            sharedSliders.Clear();
            flushSliders.Clear();
            cancelSliders.Clear();
            emptyLabel.gameObject.SetActive(true);
        }

        public void Refresh(bool visible, bool canInteract)
        {
            root.SetActive(visible);
            toggleButton.interactable = canInteract;
            if (!visible || !canInteract)
            {
                Close();
            }
            foreach (var refresh in sharedSliders)
            {
                refresh(canInteract);
            }
            foreach (var control in controlStates)
            {
                control.Control.interactable = canInteract && control.CanUse();
            }
            foreach (var action in actions)
            {
                action.Button.interactable = canInteract && action.CanExecute();
            }
        }

        public void Toggle()
        {
            if (!toggleButton.interactable)
            {
                return;
            }
            bool open = !IsOpen;
            panel.SetActive(open);
            toggleLabel.text = open ? "기능 메뉴 닫기" : "기능 메뉴";
        }

        public void Close()
        {
            if (panel == null)
            {
                return;
            }
            panel.SetActive(false);
            toggleLabel.text = "기능 메뉴";
        }

        private Button CreateButton(string text, Transform parent)
        {
            var button = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            button.transform.SetParent(parent, false);
            button.GetComponent<Image>().color = new Color32(51, 65, 85, 255);
            button.targetGraphic = button.GetComponent<Image>();
            UguiVisualStyle.Round(button.GetComponent<Image>());
            var label = CreateLabel(text, button.transform);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private Text CreateLabel(string text, Transform parent)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.font = font;
            label.fontSize = 18;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.supportRichText = false;
            label.text = text;
            return label;
        }
    }
}
