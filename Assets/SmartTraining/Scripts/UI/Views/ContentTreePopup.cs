using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmartTraining
{
    // 기존 방 생성 프리팹에서도 사용할 수 있도록 선택 창을 한 번 만들고 재사용합니다.
    // 콘텐츠 데이터와 서버 요청은 Controller가 담당하고 이 객체는 펼침 상태와 선택 표시만 관리합니다.
    internal sealed class ContentTreePopup
    {
        private readonly GameObject overlay;

        private readonly RectTransform rows;

        private readonly Text selection;

        private readonly Button apply;

        private readonly Font font;

        private readonly Action<ContentSummary> onSelect;

        private readonly HashSet<string> collapsed = new HashSet<string>();

        private readonly List<(ContentSummary Content, Image Image)> highlights = new List<(ContentSummary, Image)>();

        private ContentSummary[] source = Array.Empty<ContentSummary>();

        private ContentSummary pending;

        internal bool IsOpen => overlay.activeSelf;

        // 선택 창을 한 번 구성합니다. 기존 방 생성 프리팹을 다시 생성하지 않고도 동작하도록 부모 아래에 추가합니다.
        internal ContentTreePopup(Transform parent, Font font, Action<ContentSummary> onSelect)
        {
            this.font = font;
            this.onSelect = onSelect;
            var root = Node("Content Tree Overlay", parent);
            Stretch(root, Vector2.zero, Vector2.one);
            overlay = root.gameObject;
            overlay.AddComponent<Image>().color = new Color(0, 0, 0, .6f);
            var card = Node("Content Tree Card", root);
            Stretch(card, new Vector2(.12f, .10f), new Vector2(.88f, .90f));
            card.gameObject.AddComponent<Image>().color = new Color32(248, 250, 252, 255);
            var title = Label("콘텐츠 선택", card, 23);
            Stretch(title.rectTransform, new Vector2(0, .91f), Vector2.one, 18);
            title.rectTransform.offsetMin = new Vector2(18, 2);
            title.rectTransform.offsetMax = new Vector2(-18, -2);
            var hint = Label("왼쪽 + / - 버튼으로 펼치거나 접고, 콘텐츠 이름을 눌러 선택하세요.", card, 15);
            Stretch(hint.rectTransform, new Vector2(0, .84f), new Vector2(1, .91f), 18);
            hint.rectTransform.offsetMin = new Vector2(18, 2);
            hint.rectTransform.offsetMax = new Vector2(-18, -2);
            var viewport = Node("Content Tree Viewport", card);
            Stretch(viewport, new Vector2(0, .20f), new Vector2(1, .84f), 18);
            viewport.gameObject.AddComponent<Image>().color = Color.white;
            viewport.gameObject.AddComponent<RectMask2D>();
            rows = Node("Content Tree Rows", viewport);
            rows.anchorMin = new Vector2(0, 1);
            rows.anchorMax = Vector2.one;
            rows.pivot = new Vector2(.5f, 1);
            rows.sizeDelta = Vector2.zero;
            var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 4;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = rows;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            selection = Label("", card, 16);
            Stretch(selection.rectTransform, new Vector2(0, .11f), new Vector2(1, .20f), 18);
            selection.rectTransform.offsetMin = new Vector2(18, 2);
            selection.rectTransform.offsetMax = new Vector2(-18, -2);
            apply = Button(
                "선택 적용",
                card,
                () =>
            {
                if (pending == null)
                {
                    return;
                }

                onSelect(pending);
                Close();
            }
            );
            Stretch((RectTransform)apply.transform, new Vector2(.50f, 0), new Vector2(.76f, .11f), 12);
            var cancel = Button("취소", card, Close);
            Stretch((RectTransform)cancel.transform, new Vector2(.76f, 0), new Vector2(1, .11f), 12);
            overlay.SetActive(false);
        }

        // 현재 선택을 임시 선택으로 복사하고 모든 가지를 펼쳐 엽니다. 아직 Controller의 선택은 바꾸지 않습니다.
        internal void Show(ContentSummary[] contents, string rootId, int nodeId)
        {
            source = contents;
            pending = Array.Find(contents, item => item.rootContent == rootId && item.id == nodeId);
            collapsed.Clear();
            overlay.SetActive(true);
            overlay.transform.SetAsLastSibling();
            Rebuild();
            rows.anchoredPosition = Vector2.zero;
        }

        // 새 조회 결과로 목록이 교체되면 이전 노드를 적용하지 않도록 선택 창을 닫습니다.
        internal void Refresh(ContentSummary[] contents)
        {
            // 조회 결과가 교체되면 예전 데이터로 선택을 적용하지 않고 다시 열도록 합니다.
            if (IsOpen && !ReferenceEquals(source, contents))
            {
                Close();
            }
        }

        // 창만 닫습니다. 취소 시 임시 선택은 Controller에 전달되지 않습니다.
        internal void Close()
        {
            overlay.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(null);
        }

        // 노드 ID는 루트마다 겹칠 수 있으므로 루트와 숫자 ID를 묶어 펼침 상태를 구분합니다.
        private static string Key(ContentSummary content)
        {
            return content.rootContent + ":" + content.id;
        }

        // 기존 행을 비활성화한 뒤 보이는 가지의 행만 다시 만듭니다. 예약·API 상태에는 영향을 주지 않습니다.
        private void Rebuild()
        {
            foreach (Transform row in rows)
            {
                row.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(row.gameObject);
            }

            highlights.Clear();
            foreach (var content in source.Where(item => item.depth == 0))
            {
                AddRow(content, 0);
            }

            RefreshSelection();
        }

        // 현재 노드의 행을 만들고 접히지 않은 자식을 재귀적으로 추가합니다. 펼침 버튼과 선택 버튼을 분리합니다.
        private void AddRow(ContentSummary content, int depth)
        {
            var row = Node("Content " + Key(content), rows);
            var height = row.gameObject.AddComponent<LayoutElement>();
            height.minHeight = height.preferredHeight = 44;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(Math.Min(depth, 8) * 22, 0, 0, 0);
            layout.spacing = 4;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            bool children = content.children != null && content.children.Length > 0;
            bool folded = collapsed.Contains(Key(content));
            var toggle = Button(
                children ? (folded ? "+" : "-") : "·",
                row,
                () =>
            {
                if (!collapsed.Add(Key(content)))
                {
                    collapsed.Remove(Key(content));
                }

                Rebuild();
            }
            );
            var toggleSize = toggle.gameObject.AddComponent<LayoutElement>();
            toggleSize.minWidth = toggleSize.preferredWidth = 36;
            toggleSize.minHeight = toggleSize.preferredHeight = 44;
            toggle.interactable = children;
            var choose = Button(
                content.title,
                row,
                () =>
            {
                pending = content;
                RefreshSelection();
            }
            );
            choose.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            var chooseSize = choose.gameObject.AddComponent<LayoutElement>();
            chooseSize.minWidth = 60;
            chooseSize.flexibleWidth = 1;
            chooseSize.minHeight = chooseSize.preferredHeight = 44;
            highlights.Add((content, choose.GetComponent<Image>()));
            if (children && !folded)
            {
                foreach (var child in content.children)
                {
                    AddRow(child, depth + 1);
                }
            }
        }

        // 임시 선택 제목과 강조색을 갱신합니다. 실제 반영은 선택 적용 버튼에서만 수행합니다.
        private void RefreshSelection()
        {
            selection.text = "선택한 콘텐츠: " + (pending?.title ?? "없음");
            apply.interactable = pending != null;
            foreach (var item in highlights)
            {
                item.Image.color = item.Content == pending ? new Color32(191, 219, 254, 255) : new Color32(226, 232, 240, 255);
            }
        }

        // 부모 아래에 UGUI 배치용 RectTransform을 만듭니다.
        private static RectTransform Node(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            node.SetParent(parent, false);
            return node;
        }

        // 앵커 영역과 안쪽 여백을 지정해 창 크기에 맞춰 배치합니다.
        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, float padding = 0)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        // 선택 창의 제목·안내·항목 이름에 사용할 Text를 만듭니다. 마우스 입력은 가리지 않습니다.
        private Text Label(string text, Transform parent, int size)
        {
            var label = Node("Label", parent).gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.color = new Color32(30, 41, 59, 255);
            label.text = text;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            return label;
        }

        // 배경 Image와 Text를 가진 버튼을 만들고 클릭 동작을 연결합니다.
        private Button Button(string text, Transform parent, UnityEngine.Events.UnityAction action)
        {
            var node = Node(text, parent);
            var image = node.gameObject.AddComponent<Image>();
            image.color = new Color32(226, 232, 240, 255);
            var button = node.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var label = Label(text, node, 17);
            label.alignment = TextAnchor.MiddleCenter;
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, 6);
            return button;
        }
    }
}
