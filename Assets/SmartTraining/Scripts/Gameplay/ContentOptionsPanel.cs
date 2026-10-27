using System;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // 설정 창의 UI만 담당합니다. 변경 적용과 저장 시점은 ContentSceneSession에서 관리합니다.
    internal sealed class ContentOptionsPanel
    {
        private readonly Font font;

        private readonly ContentPreferences preferences;

        private readonly Action changed;

        private readonly Slider sensitivity, fieldOfView, volume;

        private readonly Text sensitivityLabel, fieldOfViewLabel, volumeLabel, invertLabel;

        public GameObject Root { get; }

        public ContentOptionsPanel(Transform parent, Font font, ContentPreferences preferences, Action changed, Action save, Action cancel)
        {
            this.font = font;
            this.preferences = preferences;
            this.changed = changed;
            Root = new GameObject("Content Options", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            Root.transform.SetParent(parent, false);
            var rect = Root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(560, 700);
            Root.GetComponent<Image>().color = new Color32(30, 41, 59, 255);
            var layout = Root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 20, 20);
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = Label("설정", Root.transform, 36);
            title.fontSize = 24;
            Label("저장 전 변경은 임시 적용됩니다. Esc를 누르면 취소됩니다.", Root.transform, 28);
            sensitivityLabel = Label("", Root.transform, 24);
            sensitivity = Slider(0.2f, 3f, value => preferences.Sensitivity = value);
            fieldOfViewLabel = Label("", Root.transform, 24);
            fieldOfView = Slider(60, 100, value => preferences.FieldOfView = value);
            volumeLabel = Label("", Root.transform, 24);
            volume = Slider(0, 1, value => preferences.Volume = value);
            invertLabel = Button("", () =>
            {
                preferences.InvertY = !preferences.InvertY;
                Apply();
            });
            Button("기본값으로 복원", () =>
            {
                preferences.Reset();
                Apply();
            });
            Button("저장하고 돌아가기", () => save());
            Button("취소하고 돌아가기", () => cancel());
            Refresh();
            Root.SetActive(false);
        }

        private void Apply()
        {
            changed();
            Refresh();
        }

        internal void Refresh()
        {
            // UI를 갱신할 때는 변경 이벤트를 다시 호출하지 않습니다.
            sensitivity.SetValueWithoutNotify(preferences.Sensitivity);
            fieldOfView.SetValueWithoutNotify(preferences.FieldOfView);
            volume.SetValueWithoutNotify(preferences.Volume);
            sensitivityLabel.text = $"마우스 감도: {preferences.Sensitivity:F1}배";
            fieldOfViewLabel.text = $"시야각 (FOV): {preferences.FieldOfView:F0}°";
            volumeLabel.text = $"전체 음량: {preferences.Volume * 100:F0}%";
            invertLabel.text = "시점 Y축 반전: " + (preferences.InvertY ? "켜짐" : "꺼짐");
        }

        private Text Label(string value, Transform parent, float height)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.font = font;
            label.fontSize = 18;
            label.color = Color.white;
            label.text = value;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            label.GetComponent<LayoutElement>().preferredHeight = height;
            return label;
        }

        private Text Button(string value, UnityEngine.Events.UnityAction action)
        {
            var button = new GameObject("Option Button", typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button), typeof(LayoutElement)).GetComponent<UnityEngine.UI.Button>();
            button.transform.SetParent(Root.transform, false);
            button.targetGraphic = button.GetComponent<Image>();
            button.GetComponent<Image>().color = new Color32(51, 65, 85, 255);
            button.GetComponent<LayoutElement>().preferredHeight = 44;
            var label = Label(value, button.transform, 44);
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            button.onClick.AddListener(action);
            return label;
        }

        private Slider Slider(float minimum, float maximum, Action<float> setValue)
        {
            var slider = new GameObject("Option Slider", typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Slider), typeof(LayoutElement)).GetComponent<UnityEngine.UI.Slider>();
            slider.transform.SetParent(Root.transform, false);
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
            handle.anchorMin = new Vector2(0, 0);
            handle.anchorMax = new Vector2(0, 1);
            handle.sizeDelta = new Vector2(20, 0);
            handle.GetComponent<Image>().color = new Color32(96, 165, 250, 255);
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.handleRect = handle;
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.onValueChanged.AddListener(value =>
            {
                setValue(value);
                Apply();
            });
            return slider;
        }
    }
}
