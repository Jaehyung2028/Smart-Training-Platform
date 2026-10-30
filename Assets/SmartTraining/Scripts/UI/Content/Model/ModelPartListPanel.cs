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
        private readonly List<Row> rows = new();
        private ModelViewer viewer;
        private readonly HashSet<ModelPartTree.Node> expanded = new();
        private int displayedVersion = -1;
        private bool revealSelection;

        public bool IsOpen { get; private set; }


        public ModelPartListPanel(Transform parent, Font font)
        {
            var view = UiPrefab.Bind("ModelPartListPanel", parent);
            root = view.Get<GameObject>("root");
            content = view.Get<RectTransform>("content");
            scroll = view.Get<ScrollRect>("scroll");
            view.Get<Button>("Close Part List:Button").onClick.AddListener(Close);
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
                row.Button.GetComponent<Image>().color = selected ? new Color32(146, 64, 14, 255) : included ? new Color32(90, 58, 35, 255) : new Color32(51, 65, 85, 255);
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
                    UiPrefab.Destroy(row.Root);
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
                var prefab = UiPrefab.Instantiate("ModelPartRow", content);
                var rowRoot = prefab.gameObject;
                rowRoot.name = "Node - " + node.Transform.name;
                rowRoot.GetComponent<HorizontalLayoutGroup>().padding.left = node.Depth * 14;
                var fold = prefab.Get<Button>("fold");
                var button = prefab.Get<Button>("choose");
                fold.name = "Expand - " + node.Transform.name;
                button.name = "Part - " + node.Transform.name;
                button.GetComponentInChildren<Text>(true).text = caption;
                fold.GetComponent<Image>().raycastTarget = node.Children.Count > 0;
                if (node.Children.Count == 0)
                {
                    fold.GetComponent<Image>().color = Color.clear;
                }
                else
                {
                    expanded.Add(node);
                }

                var row = new Row
                {
                    Root = rowRoot,
                    Node = node,
                    Button = button,
                    Fold = fold,
                    Label = button.GetComponentInChildren<Text>(),
                    FoldLabel = fold.GetComponentInChildren<Text>(),
                    Caption = caption
                };
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
    }
}
