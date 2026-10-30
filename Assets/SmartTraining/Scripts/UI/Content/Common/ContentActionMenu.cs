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
        private readonly Button toggleButton;
        private readonly Text toggleLabel;
        private readonly Text emptyLabel;
        private readonly List<(Button Button, Func<bool> CanExecute)> actions = new();
        private readonly List<GameObject> extraControls = new();
        private readonly List<(Selectable Control, Func<bool> CanUse)> controlStates = new();
        private readonly List<Action<bool>> sharedSliders = new();
        private readonly List<Action> flushSliders = new();

        public bool IsOpen => panel != null && panel.activeSelf;


        public ContentActionMenu(Transform parent, Font font)
        {
            var view = UiPrefab.Bind("ContentActionMenu", parent);
            root = view.Get<GameObject>("root");
            panel = view.Get<GameObject>("panel");
            toggleButton = view.Get<Button>("toggleButton");
            toggleLabel = view.Get<Text>("toggleLabel");
            emptyLabel = view.Get<Text>("emptyLabel");
            toggleButton.onClick.AddListener(Toggle);
            Close();
        }


        // 콘텐츠 전용 UI도 같은 기능 메뉴의 배치·닫기·정리 수명주기에 포함합니다.
        public RectTransform AddSection(string name)
        {
            var section = UiPrefab.Instantiate("ActionSection", panel.transform);
            section.name = name;
            extraControls.Add(section.gameObject);
            emptyLabel.gameObject.SetActive(false);
            return (RectTransform)section.transform;
        }


        public void AddAction(string label, Action execute, Func<bool> canExecute)
        {
            var button = CreateButton(label, panel.transform);
            button.gameObject.GetComponent<LayoutElement>().preferredHeight = 44;
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
            toggle.gameObject.GetComponent<LayoutElement>().preferredHeight = 44;
            var caption = CreateLabel("주변 부품 투명도: 70%", panel.transform);
            caption.gameObject.GetComponent<LayoutElement>().preferredHeight = 28;
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
            var slider = UiPrefab.Instantiate("ActionSlider", panel.transform).GetComponent<Slider>();
            slider.name = name;
            return slider;
        }


        // 서버의 공통 값을 표시합니다. 드래그 중 최신 입력만 0.1초 간격으로 보내고 마지막 값도 전송합니다.
        public void AddSharedSlider(
            string title,
            string name,
            Func<float> getValue,
            Action<float> setValue,
            Func<bool> canUse
        )
        {
            var caption = CreateLabel(title + ": 0%", panel.transform);
            caption.gameObject.GetComponent<LayoutElement>().preferredHeight = 28;
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
            status.gameObject.GetComponent<LayoutElement>().preferredHeight = 28;
            var rowPrefab = UiPrefab.Instantiate("ModelPlaybackControls", panel.transform);
            var row = rowPrefab.gameObject;
            foreach (var item in new[]
            {
                (Label: "분해 재생", Direction: 1),
                (Label: "일시정지", Direction: 0),
                (Label: "재조립", Direction: -1)
            }

            )
            {
                int direction = item.Direction;
                var button = rowPrefab.Get<Button>(item.Label + ":Button");
                button.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1;
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


        public void ClearActions()
        {
            Close();
            foreach (var action in actions)
            {
                action.Button.gameObject.SetActive(false);
                UiPrefab.Destroy(action.Button.gameObject);
            }

            actions.Clear();
            foreach (var control in extraControls)
            {
                control.SetActive(false);
                UiPrefab.Destroy(control);
            }

            extraControls.Clear();
            controlStates.Clear();
            sharedSliders.Clear();
            flushSliders.Clear();
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
            var button = UiPrefab.Instantiate("ActionButton", parent).GetComponent<Button>();
            button.name = text;
            button.GetComponentInChildren<Text>(true).text = text;
            return button;
        }


        private Text CreateLabel(string text, Transform parent)
        {
            var label = UiPrefab.Instantiate("ActionLabel", parent).GetComponent<Text>();
            label.text = text;
            return label;
        }
    }
}
