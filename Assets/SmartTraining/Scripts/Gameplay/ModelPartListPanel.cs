using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // 개인별 부품 목록입니다. 목록과 모델 클릭 모두 ModelViewer의 같은 선택 경로를 사용합니다.
    public sealed class ModelPartListPanel
    {
        private sealed class Row
        {
            public GameObject Root;
            public ModelPartTree.Node Node;
            public Button Button, Fold;
            public Text Label, FoldLabel;
            public string Caption;
        }

        private readonly GameObject root;
        private readonly RectTransform content;
        private readonly ScrollRect scroll;
        private readonly Font font;
        private readonly List<Row> rows = new();
        private ModelViewer viewer;
        private readonly HashSet<ModelPartTree.Node> expanded = new();
        private int displayedVersion = -1;
        private bool revealSelection;

        public bool IsOpen { get; private set; }

        public ModelPartListPanel(Transform parent, Font font)
        {
            this.font = font;
            root = new GameObject("Model Part List", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-288, -132);
            rect.sizeDelta = new Vector2(330, 380);
            root.GetComponent<Image>().color = new Color32(15, 23, 42, 245);
            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            header.transform.SetParent(root.transform, false);
            header.GetComponent<LayoutElement>().preferredHeight = 36;
            var headerLayout = header.GetComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 8;
            headerLayout.childControlWidth = headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            var title = CreateLabel("부품 목록", header.transform);
            title.fontSize = 22;
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var close = CreateButton("Close Part List", "닫기", header.transform);
            close.gameObject.AddComponent<LayoutElement>().preferredWidth = 64;
            close.onClick.AddListener(Close);

            var scrollObject = new GameObject("Part List Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
            scrollObject.transform.SetParent(root.transform, false);
            scrollObject.GetComponent<LayoutElement>().preferredHeight = 300;
            scrollObject.GetComponent<Image>().color = Color.clear;
            scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollObject.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect);
            content = new GameObject("Part Rows", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            content.SetParent(viewport.transform, false);
            content.anchorMin = Vector2.up;
            content.anchorMax = Vector2.up;
            content.pivot = Vector2.up;
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var rowLayout = content.GetComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 8;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            content.gameObject.AddComponent<LayoutElement>().minWidth = 298;
            content.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewportRect;
            scroll.content = content;
            Close();
        }

        public void Toggle()
        {
            IsOpen = !IsOpen;
            revealSelection = IsOpen;
            root.SetActive(IsOpen);
        }

        public void Close()
        {
            IsOpen = false;
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        internal void Refresh(ModelViewer currentViewer, bool visible, bool canInteract)
        {
            if (viewer != currentViewer)
            {
                Rebuild(currentViewer);
            }
            if (!visible || viewer == null)
            {
                Close();
                return;
            }
            root.SetActive(IsOpen);
            bool reveal = IsOpen && (revealSelection || displayedVersion != viewer.SelectionVersion);
            if (reveal)
            {
                foreach (var selected in viewer.SelectedNodes)
                {
                    for (var parent = selected.Parent; parent != null; parent = parent.Parent)
                    {
                        expanded.Add(parent);
                    }
                }
            }
            Row selectedRow = null;
            int selectedIndex = -1;
            int visibleCount = 0;
            foreach (var row in rows)
            {
                bool rowVisible = AncestorsExpanded(row.Node);
                row.Root.SetActive(rowVisible);
                bool selected = viewer.IsSelectedNode(row.Node);
                bool included = !selected && viewer.IsIncludedNode(row.Node);
                row.Button.interactable = canInteract && row.Node.Transform != null && row.Node.Transform.gameObject.activeInHierarchy;
                row.Fold.interactable = canInteract && row.Node.Children.Count > 0;
                row.FoldLabel.text = row.Node.Children.Count == 0 ? "" : expanded.Contains(row.Node) ? "-" : "+";
                row.Label.text = selected ? "선택 · " + row.Caption : included ? "포함 · " + row.Caption : row.Caption;
                row.Button.GetComponent<Image>().color = selected ? new Color32(146, 64, 14, 255) :
                    included ? new Color32(90, 58, 35, 255) : new Color32(51, 65, 85, 255);
                if (rowVisible)
                {
                    if (selected && selectedRow == null)
                    {
                        selectedIndex = visibleCount;
                        selectedRow = row;
                    }
                    visibleCount++;
                }
            }
            if (reveal && selectedIndex >= 0)
            {
                // 접기 상태에 따라 실제로 표시되는 행 수를 기준으로 스크롤합니다.
                Canvas.ForceUpdateCanvases();
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 1f - selectedIndex / (float)Mathf.Max(1, visibleCount - 1);
                // 깊은 가지도 선택한 이름이 가로 화면 밖으로 사라지지 않도록 합니다.
                var selectedRect = selectedRow.Button.GetComponent<RectTransform>();
                float left = content.InverseTransformPoint(selectedRect.TransformPoint(new Vector3(selectedRect.rect.xMin, 0, 0))).x;
                float width = scroll.viewport.rect.width;
                float offset = -content.anchoredPosition.x;
                float desired = offset;
                if (left < offset)
                {
                    desired = left;
                }
                else if (left + Mathf.Min(220, width) > offset + width)
                {
                    desired = left + Mathf.Min(220, width) - width;
                }
                float maximum = Mathf.Max(0, content.rect.width - width);
                scroll.horizontalNormalizedPosition = maximum > 0 ? Mathf.Clamp01(desired / maximum) : 0;
            }
            if (IsOpen)
            {
                revealSelection = false;
                displayedVersion = viewer.SelectionVersion;
            }
        }

        private bool AncestorsExpanded(ModelPartTree.Node node)
        {
            for (var parent = node.Parent; parent != null; parent = parent.Parent)
            {
                if (!expanded.Contains(parent))
                {
                    return false;
                }
            }
            return true;
        }

        public void Clear()
        {
            Rebuild(null);
        }

        private void Rebuild(ModelViewer currentViewer)
        {
            Close();
            foreach (var row in rows)
            {
                if (row.Root != null)
                {
                    row.Root.SetActive(false);
                    UnityEngine.Object.Destroy(row.Root);
                }
            }
            rows.Clear();
            expanded.Clear();
            viewer = currentViewer;
            displayedVersion = -1;
            if (content != null)
            {
                content.anchoredPosition = Vector2.zero;
            }
            if (viewer == null)
            {
                return;
            }
            var counts = new Dictionary<string, int>();
            foreach (var node in viewer.Parts)
            {
                counts[node.Title] = counts.TryGetValue(node.Title, out var count) ? count + 1 : 1;
            }
            var indices = new Dictionary<string, int>();
            foreach (var node in viewer.Parts)
            {
                string title = node.Title;
                indices[title] = indices.TryGetValue(title, out var index) ? index + 1 : 1;
                string caption = counts[title] > 1 ? title + " " + indices[title] : title;
                var rowRoot = new GameObject("Node - " + node.Transform.name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                rowRoot.transform.SetParent(content, false);
                rowRoot.GetComponent<LayoutElement>().preferredHeight = 44;
                var layout = rowRoot.GetComponent<HorizontalLayoutGroup>();
                layout.padding.left = node.Depth * 14;
                layout.spacing = 4;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                var fold = CreateButton("Expand - " + node.Transform.name, "", rowRoot.transform);
                fold.gameObject.AddComponent<LayoutElement>().preferredWidth = 24;
                fold.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
                fold.GetComponent<Image>().raycastTarget = node.Children.Count > 0;
                if (node.Children.Count == 0)
                {
                    fold.GetComponent<Image>().color = Color.clear;
                }
                else
                {
                    expanded.Add(node);
                }
                var button = CreateButton("Part - " + node.Transform.name, caption, rowRoot.transform);
                var size = button.gameObject.AddComponent<LayoutElement>();
                size.preferredWidth = 220;
                size.minWidth = 140;
                size.flexibleWidth = 1;
                var row = new Row { Root = rowRoot, Node = node, Button = button, Fold = fold,
                    Label = button.GetComponentInChildren<Text>(), FoldLabel = fold.GetComponentInChildren<Text>(), Caption = caption };
                button.onClick.AddListener(() =>
                {
                    if (row.Button.interactable && viewer != null)
                    {
                        viewer.SelectPartFromList(row.Node);
                    }
                });
                fold.onClick.AddListener(() =>
                {
                    if (!row.Fold.interactable)
                    {
                        return;
                    }
                    if (!expanded.Remove(row.Node))
                    {
                        expanded.Add(row.Node);
                    }
                    revealSelection = false;
                    displayedVersion = viewer.SelectionVersion;
                });
                rows.Add(row);
            }
        }

        private Button CreateButton(string name, string caption, Transform parent)
        {
            var button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            button.transform.SetParent(parent, false);
            button.GetComponent<Image>().color = new Color32(51, 65, 85, 255);
            button.targetGraphic = button.GetComponent<Image>();
            var label = CreateLabel(caption, button.transform);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 14;
            label.resizeTextMaxSize = 18;
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(8, 0);
            label.rectTransform.offsetMax = new Vector2(-8, 0);
            return button;
        }

        private Text CreateLabel(string caption, Transform parent)
        {
            var text = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.text = caption;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
