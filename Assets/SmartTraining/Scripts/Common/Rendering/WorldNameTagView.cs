using System;
using UnityEngine;

namespace SmartTraining
{
    // 전달받은 프리팹을 표시하고 텍스트·빌보드만 갱신합니다. UI 오브젝트나 디자인은 코드로 만들지 않습니다.
    public sealed class WorldNameTagView : IDisposable
    {
        private readonly WorldNameTagReferences view;
        private readonly RectTransform rect;
        private readonly Vector2 baseSize;
        private readonly Vector2 nameOffsetMin;
        private readonly Vector2 detailOffsetMax;
        private string displayedName, displayedDetail;

        public WorldNameTagView(Transform parent, WorldNameTagReferences prefab)
        {
            view = UnityEngine.Object.Instantiate(prefab, parent, false);
            rect = (RectTransform)view.transform;
            baseSize = rect.sizeDelta;
            nameOffsetMin = view.NameLabel.rectTransform.offsetMin;
            detailOffsetMax = view.DetailLabel.rectTransform.offsetMax;
            view.gameObject.SetActive(false);
        }


        public void Refresh(string displayName, Camera viewCamera, string detail = "")
        {
            bool visible = viewCamera != null && !string.IsNullOrEmpty(displayName);
            view.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            if (displayedName != displayName || displayedDetail != detail)
            {
                displayedName = displayName;
                displayedDetail = detail;
                view.NameLabel.text = displayName;
                bool hasDetail = !string.IsNullOrEmpty(detail);
                int lines = hasDetail ? 1 : 0;
                if (hasDetail)
                {
                    foreach (char character in detail)
                    {
                        if (character == '\n')
                        {
                            lines++;
                        }
                    }
                }

                // 프리팹에서 지정한 폭·여백·기본 높이는 유지하고 역할에 필요한 높이만 더합니다.
                float height = Mathf.Min(lines, 4) * view.DetailLineHeight;
                rect.sizeDelta = baseSize + Vector2.up * height;
                view.NameLabel.rectTransform.offsetMin = nameOffsetMin + Vector2.up * height;
                view.DetailLabel.rectTransform.offsetMax = detailOffsetMax + Vector2.up * height;
                view.DetailLabel.gameObject.SetActive(hasDetail);
                view.DetailLabel.text = detail;
            }

            view.Canvas.worldCamera = viewCamera;
            view.transform.rotation = viewCamera.transform.rotation;
        }


        public void Dispose()
        {
            view.gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(view.gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }
    }
}
