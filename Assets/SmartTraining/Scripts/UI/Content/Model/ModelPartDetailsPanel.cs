using System;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // 부품 설명은 각 참가자의 HUD에서만 표시합니다. 창을 열어도 이동과 모델 회전을 중지하지 않습니다.
    public sealed class ModelPartDetailsPanel
    {
        private readonly GameObject root;
        private readonly Text title;
        private readonly Text description;
        private readonly ScrollRect scroll;
        private int displayedVersion = -1;

        public ModelPartDetailsPanel(Transform parent, Font font, Action close)
        {
            var view = UiPrefab.Bind("ModelPartDetailsPanel", parent);
            root = view.Get<GameObject>("root");
            title = view.Get<Text>("title");
            description = view.Get<Text>("description");
            scroll = view.Get<ScrollRect>("scroll");
            view.Get<Button>("Close Part Details:Button").onClick.AddListener(() => close());
            root.SetActive(false);
        }


        internal void Refresh(ModelViewer viewer, bool visible)
        {
            bool show = visible && viewer != null && viewer.SelectedPart != null;
            root.SetActive(show);
            if (!show)
            {
                return;
            }

            title.text = viewer.SelectedPartTitle;
            description.text = viewer.SelectedPartDescription;
            if (displayedVersion != viewer.SelectionVersion)
            {
                displayedVersion = viewer.SelectionVersion;
                scroll.StopMovement();
                scroll.content.anchoredPosition = Vector2.zero;
            }
        }
    }
}
