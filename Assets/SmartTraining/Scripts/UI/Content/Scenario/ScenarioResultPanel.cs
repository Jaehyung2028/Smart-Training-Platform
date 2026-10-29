using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // 완료한 현재 실습의 공동 결과입니다. 서버 기록을 표시하고 기존 재실습·대기 복귀 동작만 호출합니다.
    internal sealed class ScenarioResultPanel
    {
        private readonly GameObject root;
        private readonly RectTransform card, content;
        private readonly Font font;
        private readonly Text heading, summary, hint;
        private readonly Button restart, returnToWaiting;
        private readonly List<GameObject> rows = new();
        private readonly Action returnAction;
        private ScenarioContent scenario;
        private int displayedRun = -1, displayedRows = -1;
        private bool canUse, canReturn;

        public ScenarioResultPanel(Transform parent, Font font, Action returnAction)
        {
            this.font = font;
            this.returnAction = returnAction;
            root = Box("Scenario Results", parent, new Color(0, 0, 0, .78f)).gameObject;
            Stretch(root.GetComponent<RectTransform>(), 0, 0);
            card = Box("Scenario Result Card", root.transform, new Color32(15, 23, 42, 252)).rectTransform;
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            card.sizeDelta = new Vector2(960, 590);
            var title = Label("Scenario Result Title", card, 28);
            Place(title.rectTransform, 24, 20, 36);
            title.text = "실습 완료";
            title.color = new Color32(74, 222, 128, 255);
            heading = Label("Scenario Result Content", card, 18);
            Place(heading.rectTransform, 24, 62, 26);
            summary = Label("Scenario Result Summary", card, 19);
            Place(summary.rectTransform, 24, 100, 40);
            Header("단계", 0, .44f);
            Header("수행자 · 소요 시간", .44f, .72f);
            Header("공구 오류 · 대상 오류", .72f, 1);
            var viewport = new GameObject(
                "Scenario Result Viewport",
                typeof(RectTransform),
                typeof(Image),
                typeof(Mask),
                typeof(ScrollRect)
            );
            viewport.transform.SetParent(card, false);
            var rect = viewport.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(24, 128);
            rect.offsetMax = new Vector2(-24, -190);
            viewport.GetComponent<Image>().color = new Color32(30, 41, 59, 255);
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = rect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            content = new GameObject(
                "Scenario Result Rows",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter)
            ).GetComponent<RectTransform>();
            content.SetParent(rect, false);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0, 1);
            content.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            hint = Label("Scenario Result Hint", card, 13);
            hint.rectTransform.anchorMin = new Vector2(0, 0);
            hint.rectTransform.anchorMax = new Vector2(1, 0);
            hint.rectTransform.offsetMin = new Vector2(24, 76);
            hint.rectTransform.offsetMax = new Vector2(-24, 118);
            hint.resizeTextForBestFit = true;
            hint.resizeTextMinSize = 11;
            hint.resizeTextMaxSize = 13;
            hint.color = new Color32(148, 163, 184, 255);
            restart = CreateButton("Scenario Result Restart", "다시 실습 (방장)", 0, .5f, () =>
            {
                if (canUse && scenario != null && scenario.IsComplete && scenario.CanReset)
                {
                    scenario.ResetPractice();
                }
            });
            returnToWaiting = CreateButton("Scenario Result Return", "대기실로 복귀 (방장)", .5f, 1, () =>
            {
                if (canUse && canReturn && scenario != null && scenario.IsComplete && scenario.CanReset)
                {
                    this.returnAction?.Invoke();
                }
            });
            root.SetActive(false);
        }


        public void Refresh(ScenarioContent current, bool visible, bool canInteract, bool canReturn)
        {
            if (scenario != current)
            {
                scenario = current;
                displayedRun = displayedRows = -1;
            }

            root.SetActive(visible && current != null && current.IsComplete);
            if (!root.activeSelf)
            {
                return;
            }

            var size = root.GetComponent<RectTransform>().rect.size;
            if (size.x <= 0 || size.y <= 0)
            {
                size = new Vector2(Screen.width, Screen.height);
            }

            card.sizeDelta = new Vector2(Mathf.Min(960, size.x - 32), Mathf.Min(590, size.y - 88));
            heading.text = current.Definition.Title;
            var outcomes = current.Outcomes.ToArray();
            bool ready = outcomes.Length == current.Definition.Steps.Count;
            canUse = ready && canInteract;
            this.canReturn = canReturn;
            summary.text = ready ? "총 소요 시간 " + Duration(current.ElapsedSeconds) + "    ·    공구 오류 " + current.ToolErrors + "    ·    대상 오류 " + current.TargetErrors : "서버의 완료 결과를 수신하고 있습니다…";
            summary.resizeTextForBestFit = true;
            summary.resizeTextMinSize = 13;
            summary.resizeTextMaxSize = 19;
            hint.text = "오류는 해당 단계의 전체 참가자 합계입니다. 공구 미선택도 포함하며, 작업 시도 시 집계합니다.\n방장이 다시 실습하거나 대기실로 복귀할 수 있습니다.";
            restart.interactable = canUse && current.CanReset;
            returnToWaiting.interactable = canUse && canReturn && current.CanReset;
            if (displayedRun != current.RunRevision || displayedRows != outcomes.Length)
            {
                Rebuild(outcomes);
                displayedRun = current.RunRevision;
                displayedRows = outcomes.Length;
            }
        }


        private void Rebuild(ScenarioStepOutcome[] outcomes)
        {
            foreach (var row in rows)
            {
                row.SetActive(false);
                UnityEngine.Object.Destroy(row);
            }

            rows.Clear();
            foreach (var outcome in outcomes)
            {
                var row = Box("Scenario Result Row " + outcome.Index, content, new Color32(30, 41, 59, 255)).gameObject;
                row.AddComponent<LayoutElement>().preferredHeight = 42;
                Cell(
                    row.transform,
                    "Result Step",
                    (outcome.Index + 1) + ". " + scenario.Definition.Steps[outcome.Index].Title,
                    0,
                    .44f
                );
                Cell(
                    row.transform,
                    "Result Actor",
                    outcome.ActorName + " · " + Duration(outcome.DurationSeconds),
                    .44f,
                    .72f
                );
                Cell(
                    row.transform,
                    "Result Errors",
                    "공구 " + outcome.ToolErrors + " · 대상 " + outcome.TargetErrors,
                    .72f,
                    1
                );
                rows.Add(row);
            }
        }


        private void Header(string caption, float left, float right)
        {
            var text = Label("Scenario Result Header " + caption, card, 14);
            text.text = caption;
            text.color = new Color32(148, 163, 184, 255);
            text.rectTransform.anchorMin = new Vector2(left, 1);
            text.rectTransform.anchorMax = new Vector2(right, 1);
            text.rectTransform.offsetMin = new Vector2(24, -180);
            text.rectTransform.offsetMax = new Vector2(-24, -152);
        }


        private void Cell(Transform row, string name, string value, float left, float right)
        {
            var label = Label(name, row, 16);
            label.text = value;
            label.rectTransform.anchorMin = new Vector2(left, 0);
            label.rectTransform.anchorMax = new Vector2(right, 1);
            label.rectTransform.offsetMin = new Vector2(10, 2);
            label.rectTransform.offsetMax = new Vector2(-6, -2);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = 16;
        }


        private Button CreateButton(string name, string caption, float left, float right, Action click)
        {
            var background = Box(name, card, new Color32(37, 99, 235, 255));
            var rect = background.rectTransform;
            rect.anchorMin = new Vector2(left, 0);
            rect.anchorMax = new Vector2(right, 0);
            rect.offsetMin = new Vector2(left == 0 ? 24 : 6, 24);
            rect.offsetMax = new Vector2(right == 1 ? -24 : -6, 66);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var label = Label(name + " Label", rect, 18);
            label.text = caption;
            label.alignment = TextAnchor.MiddleCenter;
            Stretch(label.rectTransform, 4, 2);
            button.onClick.AddListener(() =>
            {
                if (button.IsInteractable())
                {
                    click();
                }
            });
            return button;
        }


        private Text Label(string name, Transform parent, int size)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }


        private static Image Box(string name, Transform parent, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            return image;
        }


        private static void Place(RectTransform rect, float left, float top, float height)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-24, -top);
        }


        private static void Stretch(RectTransform rect, float x, float y)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(x, y);
            rect.offsetMax = new Vector2(-x, -y);
        }


        private static string Duration(double seconds)
        {
            var duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return $"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}";
        }
    }
}
