using System;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // 설정 창의 UI만 담당합니다. 변경 적용·저장·취소 시점은 사용하는 UI 컨트롤러가 결정합니다.
    public sealed class ClientSettingsPanel
    {
        private readonly ClientPreferences preferences;
        private readonly Action changed;
        private readonly Slider sensitivity, fieldOfView, volume;
        private readonly Text sensitivityLabel, fieldOfViewLabel, volumeLabel, invertLabel;

        public GameObject Root { get; }


        public ClientSettingsPanel(
            Transform parent,
            Font font,
            ClientPreferences preferences,
            Action changed,
            Action save,
            Action cancel
        )
        {
            var view = UiPrefab.Bind("ClientSettingsPanel", parent);
            sensitivity = view.Get<Slider>("sensitivity");
            fieldOfView = view.Get<Slider>("fieldOfView");
            volume = view.Get<Slider>("volume");
            sensitivityLabel = view.Get<Text>("sensitivityLabel");
            fieldOfViewLabel = view.Get<Text>("fieldOfViewLabel");
            volumeLabel = view.Get<Text>("volumeLabel");
            invertLabel = view.Get<Text>("invertLabel");
            Root = view.Get<GameObject>("Root");
            this.preferences = preferences;
            this.changed = changed;
            sensitivity.onValueChanged.AddListener(value =>
            {
                preferences.Sensitivity = value;
                Apply();
            });
            fieldOfView.onValueChanged.AddListener(value =>
            {
                preferences.FieldOfView = value;
                Apply();
            });
            volume.onValueChanged.AddListener(value =>
            {
                preferences.Volume = value;
                Apply();
            });
            invertLabel.GetComponentInParent<Button>(true).onClick.AddListener(() =>
            {
                preferences.InvertY = !preferences.InvertY;
                Apply();
            });
            view.Get<Button>("기본값으로 복원:Button").onClick.AddListener(() =>
            {
                preferences.Reset();
                Apply();
            });
            view.Get<Button>("저장하고 돌아가기:Button").onClick.AddListener(() => save());
            view.Get<Button>("취소하고 돌아가기:Button").onClick.AddListener(() => cancel());
            Refresh();
            Root.SetActive(false);
        }


        private void Apply()
        {
            changed();
            Refresh();
        }


        public void Refresh()
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
    }
}
