using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmartTraining
{
    // 화면 전체를 덮는 독립 선택 창입니다. 방 생성 입력값을 유지한 채 트리를 탐색합니다.
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


        // 씬·부모 프리팹에 미리 배치한 선택 창을 연결합니다. 트리 행만 데이터 개수에 따라 복제합니다.
        internal ContentTreePopup(Transform parent, Font font, Action<ContentSummary> onSelect)
        {
            var view = UiPrefab.Bind("ContentTreePopup", parent);
            overlay = view.Get<GameObject>("overlay");
            rows = view.Get<RectTransform>("rows");
            selection = view.Get<Text>("selection");
            apply = view.Get<Button>("apply");
            this.font = font;
            this.onSelect = onSelect;
            apply.onClick.AddListener(() =>
            {
                if (pending != null)
                {
                    onSelect(pending);
                    Close();
                }
            });
            view.Get<Button>("취소:Button").onClick.AddListener(Close);
            Close();
        }


        // 현재 선택을 임시 선택으로 복사하고 모든 가지를 펼쳐 엽니다. 아직 Controller의 선택은 바꾸지 않습니다.
        internal void Show(ContentSummary[] contents, string rootId, int nodeId)
        {
            source = contents;
            pending = Array.Find(
                contents,
                item => item.rootContent == rootId && item.id == nodeId && (item.children == null || item.children.Length == 0)
            );
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
            // 숨겨진 로비 화면에서도 매 프레임 호출될 수 있습니다. 이미 닫혔으면 아무것도 하지 않습니다.
            if (!overlay.activeSelf)
            {
                return;
            }

            // 방의 채팅·설정 입력창 등 다른 UI가 가진 포커스는 건드리지 않습니다.
            UguiViewUtility.ClearSelection(overlay.transform);
            overlay.SetActive(false);
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
                UiPrefab.Destroy(row.gameObject);
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
            var prefab = UiPrefab.Instantiate("ContentTreeRow", rows);
            var row = (RectTransform)prefab.transform;
            row.name = "Content " + Key(content);
            row.GetComponent<HorizontalLayoutGroup>().padding.left = Math.Min(depth, 8) * 22;
            bool children = content.children != null && content.children.Length > 0;
            bool folded = collapsed.Contains(Key(content));
            var toggle = prefab.Get<Button>("fold");
            toggle.GetComponentInChildren<Text>(true).text = children ? (folded ? "+" : "-") : "·";
            toggle.interactable = children;
            toggle.onClick.AddListener(() =>
            {
                if (!collapsed.Add(Key(content)))
                {
                    collapsed.Remove(Key(content));
                }

                Rebuild();
            });
            var choose = prefab.Get<Button>("choose");
            choose.GetComponentInChildren<Text>(true).text = content.title;
            choose.interactable = !children;
            choose.onClick.AddListener(() =>
            {
                pending = content;
                RefreshSelection();
            });
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
    // 앵커 영역과 안쪽 여백을 지정해 창 크기에 맞춰 배치합니다.
    // 선택 창의 제목·안내·항목 이름에 사용할 Text를 만듭니다. 마우스 입력은 가리지 않습니다.
    // 배경 Image와 Text를 가진 버튼을 만들고 클릭 동작을 연결합니다.
    }
}
