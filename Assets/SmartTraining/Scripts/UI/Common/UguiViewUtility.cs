using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SmartTraining
{
    // View의 공통 초기화를 모읍니다. 입장·권한·네트워크 흐름은 ApiLobbyScreen에서 처리합니다.
    internal static class UguiViewUtility
    {
        // 텍스트 입력 여부는 키보드 입력 모듈이 아닌 UGUI 포커스에서 판단합니다.
        internal static bool IsEditingText
        {
            get
            {
                var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
                return selected != null && selected.TryGetComponent<UnityEngine.UI.InputField>(out var field) && field.isFocused;
            }
        }


        internal static void EnsureComponents(
            Component view,
            ref Canvas canvas,
            ref CanvasGroup interaction,
            ref GraphicRaycaster raycaster
        )
        {
            // Unity의 null 비교로 컴포넌트 누락과 이미 파괴된 객체를 함께 확인합니다.
            if (canvas == null)
            {
                canvas = view.GetComponent<Canvas>();
            }

            if (interaction == null)
            {
                interaction = view.GetComponent<CanvasGroup>();
            }

            if (interaction == null)
            {
                throw new InvalidOperationException(view.name + " 프리팹에 CanvasGroup을 연결해주세요.");
            }

            if (raycaster == null)
            {
                raycaster = view.GetComponent<GraphicRaycaster>();
            }
        }


        internal static void SetVisible(
            Component view,
            Canvas canvas,
            CanvasGroup interaction,
            GraphicRaycaster raycaster,
            bool visible
        )
        {
            canvas.enabled = visible;
            interaction.interactable = interaction.blocksRaycasts = visible;
            raycaster.enabled = visible;
            if (!visible)
            {
                ClearSelection(view.transform);
            }
        }


        internal static void ClearSelection(Transform root)
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(root))
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }


        internal static void EnsureEventSystem(Transform parent)
        {
            if (EventSystem.current != null)
            {
                return;
            }

            UiPrefab.Instantiate("EventSystem", parent);
        }


        // View가 직접 만든 폰트만 반환합니다. 외부에서 지정한 폰트 에셋은 OnDestroy에서 파괴하지 않습니다.
        internal static Font ApplyFont(GameObject root, Font fontOverride)
        {
            var labels = root.GetComponentsInChildren<Text>(true);
            if (fontOverride == null && labels.All(label => label.font != null))
            {
                return null;
            }

            Font font = fontOverride, ownedFont = null;
            if (font == null)
            {
                var installed = new HashSet<string>(Font.GetOSInstalledFontNames(), StringComparer.OrdinalIgnoreCase);
                foreach (var name in new[]
                {
                    "Malgun Gothic",
                    "맑은 고딕",
                    "Apple SD Gothic Neo",
                    "Noto Sans CJK KR"
                }

                )
                {
                    if (installed.Contains(name))
                    {
                        font = ownedFont = Font.CreateDynamicFontFromOSFont(name, 20);
                        break;
                    }
                }
            }

            if (font != null)
            {
                foreach (var label in labels)
                {
                    if (fontOverride != null || label.font == null)
                    {
                        label.font = font;
                    }
                }
            }

            return ownedFont;
        }


        internal static bool RemoveMissing<T>(Dictionary<string, T> items, HashSet<string> ids)
            where T : Component
        {
            bool changed = false;
            foreach (var id in items.Keys.Where(id => !ids.Contains(id)).ToArray())
            {
                items[id].gameObject.SetActive(false);
                UnityEngine.Object.Destroy(items[id].gameObject);
                items.Remove(id);
                changed = true;
            }

            return changed;
        }
    }
}
