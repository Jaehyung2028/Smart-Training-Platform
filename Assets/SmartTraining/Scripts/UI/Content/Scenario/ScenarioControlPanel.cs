using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // 목표·진행·공구 조건과 작업 취소 버튼을 표시합니다. 권한·작업 판정은 서버에 맡깁니다.
    internal sealed class ScenarioControlPanel
    {
        private readonly GameObject root;
        private readonly Font font;
        private readonly Text title, progress, currentTitle, instruction, requirement, myTool, actor;
        private readonly Image requirementBackground;
        private readonly RectTransform progressFill;
        private readonly GameObject workRoot;
        private readonly Text workStatus;
        private readonly RectTransform workFill;
        private readonly Button cancelWork;
        private readonly ScrollRect stepScroll;
        private readonly RectTransform stepContent;
        private readonly List<(GameObject Root, Image Background, Text Text)> rows = new();
        private readonly GameObject feedbackRoot;
        private readonly Text feedback;
        private ScenarioContent previous;
        private ScenarioDefinition definition;
        private int lastFeedbackVersion = -1, displayedStep = -1;
        private float feedbackUntil;
        private static readonly Color Muted = new Color32(148, 163, 184, 255);
        private static readonly Color Accent = new Color32(56, 189, 248, 255);
        private static readonly Color Success = new Color32(74, 222, 128, 255);

        public ScenarioControlPanel(Transform parent, Font font)
        {
            this.font = font;
            root = new GameObject("Scenario Controls", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(24, -80);
            rect.sizeDelta = new Vector2(376, 440);
            root.GetComponent<Image>().color = new Color32(15, 23, 42, 242);
            CreateText("Scenario Eyebrow", "시나리오 실습", 13, 16, 18).color = Accent;
            title = CreateText("Scenario Title", "실습 준비 중", 22, 38, 30);
            title.fontStyle = FontStyle.Bold;
            CreateText("Scenario Progress Caption", "공동 진행", 13, 76, 20).color = Muted;
            progress = CreateText("Scenario Progress", "0 / 0 완료", 14, 76, 20);
            progress.alignment = TextAnchor.MiddleRight;
            var track = Box("Scenario Progress Track", root.transform, new Color32(51, 65, 85, 255));
            Place(track.rectTransform, 20, 104, 336, 6);
            var fill = Box("Scenario Progress Fill", track.transform, Accent);
            progressFill = fill.rectTransform;
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(0, 1);
            progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
            var divider = Box("Scenario Divider", root.transform, new Color32(51, 65, 85, 200));
            Place(divider.rectTransform, 20, 124, 336, 1);
            CreateText("Scenario Current Caption", "현재 목표", 12, 138, 16).color = Accent;
            currentTitle = CreateText("Scenario Current Title", "준비 중", 20, 160, 28);
            currentTitle.fontStyle = FontStyle.Bold;
            instruction = CreateText("Scenario Instruction", "실습 상태를 준비하고 있습니다…", 18, 196, 48);
            instruction.resizeTextForBestFit = true;
            instruction.resizeTextMinSize = 14;
            instruction.resizeTextMaxSize = 18;
            requirementBackground = Box("Scenario Requirement Badge", root.transform, new Color32(30, 58, 80, 255));
            Place(requirementBackground.rectTransform, 20, 252, 336, 28);
            requirement = Label("Scenario Requirement", requirementBackground.transform, 14);
            Stretch(requirement.rectTransform, 10, 2);
            myTool = CreateText("Scenario My Tool", "", 14, 288, 20);
            myTool.resizeTextForBestFit = true;
            myTool.resizeTextMinSize = 9;
            myTool.resizeTextMaxSize = 14;
            myTool.color = Muted;
            CreateText("Scenario Steps Caption", "진행 단계", 12, 320, 18).color = Muted;
            var viewport = new GameObject(
                "Scenario Step Viewport",
                typeof(RectTransform),
                typeof(Image),
                typeof(Mask),
                typeof(ScrollRect)
            );
            viewport.transform.SetParent(root.transform, false);
            Place(viewport.GetComponent<RectTransform>(), 20, 346, 336, 60);
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            stepScroll = viewport.GetComponent<ScrollRect>();
            stepScroll.viewport = viewport.GetComponent<RectTransform>();
            stepScroll.horizontal = false;
            stepScroll.vertical = true;
            stepScroll.movementType = ScrollRect.MovementType.Clamped;
            stepScroll.scrollSensitivity = 24;
            stepContent = new GameObject(
                "Scenario Steps",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter)
            ).GetComponent<RectTransform>();
            stepContent.SetParent(viewport.transform, false);
            stepContent.anchorMin = new Vector2(0, 1);
            stepContent.anchorMax = new Vector2(1, 1);
            stepContent.pivot = new Vector2(0, 1);
            stepContent.sizeDelta = Vector2.zero;
            var layout = stepContent.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            stepContent.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            stepScroll.content = stepContent;
            actor = CreateText("Scenario Last Actor", "진행 결과는 참가자 모두에게 공유됩니다.", 12, 414, 18);
            actor.color = Muted;
            workRoot = new GameObject("Scenario Work", typeof(RectTransform));
            workRoot.transform.SetParent(root.transform, false);
            Place(workRoot.GetComponent<RectTransform>(), 20, 440, 336, 48);
            workStatus = Label("Scenario Work Status", workRoot.transform, 13);
            Place(workStatus.rectTransform, 0, 0, 224, 20);
            workStatus.color = Accent;
            var workTrack = Box("Scenario Work Track", workRoot.transform, new Color32(51, 65, 85, 255));
            Place(workTrack.rectTransform, 0, 28, 224, 6);
            workFill = Box("Scenario Work Fill", workTrack.transform, Accent).rectTransform;
            workFill.anchorMin = Vector2.zero;
            workFill.anchorMax = new Vector2(0, 1);
            workFill.offsetMin = workFill.offsetMax = Vector2.zero;
            var cancelImage = Box("Scenario Cancel Work", workRoot.transform, new Color32(185, 60, 60, 255));
            Place(cancelImage.rectTransform, 236, 0, 100, 36);
            cancelImage.raycastTarget = true;
            UguiVisualStyle.Round(cancelImage);
            cancelWork = cancelImage.gameObject.AddComponent<Button>();
            cancelWork.targetGraphic = cancelImage;
            var cancelLabel = Label("Scenario Cancel Label", cancelImage.transform, 14);
            cancelLabel.text = "작업 취소";
            cancelLabel.alignment = TextAnchor.MiddleCenter;
            Stretch(cancelLabel.rectTransform, 4, 2);
            cancelWork.onClick.AddListener(() =>
            {
                if (cancelWork.IsInteractable())
                {
                    previous?.CancelWork();
                }
            });
            workRoot.SetActive(false);
            feedbackRoot = new GameObject("Scenario Feedback", typeof(RectTransform), typeof(Image));
            feedbackRoot.transform.SetParent(parent, false);
            var feedbackRect = feedbackRoot.GetComponent<RectTransform>();
            feedbackRect.anchorMin = feedbackRect.anchorMax = feedbackRect.pivot = new Vector2(0.5f, 0);
            feedbackRect.anchoredPosition = new Vector2(0, 24);
            feedbackRect.sizeDelta = new Vector2(660, 80);
            feedbackRoot.GetComponent<Image>().color = new Color32(30, 41, 59, 245);
            feedbackRoot.GetComponent<Image>().raycastTarget = false;
            feedback = Label("Scenario Feedback Text", feedbackRoot.transform, 20);
            feedback.color = new Color32(251, 191, 36, 255);
            feedback.alignment = TextAnchor.MiddleCenter;
            Stretch(feedback.rectTransform, 16, 8);
            feedbackRoot.SetActive(false);
            root.SetActive(false);
        }


        public void Refresh(ScenarioContent scenario, bool visible)
        {
            if (previous != scenario)
            {
                previous = scenario;
                definition = null;
                displayedStep = -1;
                lastFeedbackVersion = -1;
                feedbackUntil = 0;
            }

            root.SetActive(visible && scenario != null);
            if (scenario != null && scenario.FeedbackVersion != lastFeedbackVersion)
            {
                lastFeedbackVersion = scenario.FeedbackVersion;
                feedback.text = scenario.Feedback;
                feedbackUntil = scenario.Feedback.Length > 0 ? Time.unscaledTime + 4 : 0;
            }

            feedbackRoot.SetActive(root.activeSelf && Time.unscaledTime < feedbackUntil);
            if (!root.activeSelf)
            {
                return;
            }

            if (definition != scenario.Definition || rows.Count != scenario.Definition.Steps.Count)
            {
                RebuildSteps(scenario.Definition);
            }

            title.text = definition.Title;
            int completed = Mathf.Clamp(scenario.CompletedSteps, 0, definition.Steps.Count);
            progress.text = completed + " / " + definition.Steps.Count + " 완료";
            progressFill.anchorMax = new Vector2(completed / (float)definition.Steps.Count, 1);
            if (!scenario.IsReady)
            {
                currentTitle.text = "준비 중";
                instruction.text = "서버의 실습 상태를 준비하고 있습니다…";
                requirement.text = "진행 정보 수신 중";
                requirement.color = Muted;
                myTool.color = Muted;
                myTool.text = "";
            }
            else if (scenario.IsComplete)
            {
                currentTitle.text = "모든 목표 완료";
                instruction.text = "실습을 완료했습니다. 다시 진행하려면 방장이 기능 메뉴에서 초기화할 수 있습니다.";
                requirement.text = "실습 완료";
                requirement.color = Success;
                myTool.text = "수고하셨습니다.";
                myTool.color = Muted;
            }
            else
            {
                var step = definition.Steps[completed];
                currentTitle.text = step.Title;
                instruction.text = scenario.IsWorking ? "작업 중입니다. 완료될 때까지 기다려주세요." : step.Instruction;
                requirement.text = step.RequiresTool ? "필요 공구  ·  " + definition.GetToolTitle(step.ToolId) : "공구 불필요  ·  대상만 클릭";
                requirement.color = step.RequiresTool ? Accent : Success;
                if (definition.RequiresRoles)
                {
                    requirement.text = ScenarioRoles.Label(step.RequiredRole) + " · " + (step.RequiresTool ? definition.GetToolTitle(step.ToolId) : "공구 불필요");
                    if ((scenario.SelectedRoles & step.RequiredRole) != step.RequiredRole)
                    {
                        instruction.text += "\n담당자는 역할 메뉴에서 확인할 수 있습니다.";
                    }
                }

                myTool.text = step.RequiresTool ? "내 공구: " + definition.GetToolTitle(scenario.SelectedTool) : "공구를 선택하거나 해제할 필요가 없습니다.";
                if (definition.RequiresRoles)
                {
                    myTool.text = "내 역할: " + ScenarioRoles.Label(scenario.SelectedRoles) + " · 공구: " + definition.GetToolTitle(scenario.SelectedTool);
                }

                myTool.color = step.RequiresTool && scenario.SelectedTool != step.ToolId ? new Color32(251, 191, 36, 255) : Muted;
            }

            if (scenario.IsWorking)
            {
                requirement.text = scenario.CanCancelWork ? "내 작업 진행 중  ·  취소 가능" : "다른 참가자 작업 중";
                requirement.color = Accent;
            }

            currentTitle.color = scenario.IsComplete ? Success : Color.white;
            actor.text = scenario.LastActor.Length > 0 ? "최근 진행  ·  " + scenario.LastActor : "진행 결과는 참가자 모두에게 공유됩니다.";
            workRoot.SetActive(scenario.IsWorking);
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(376, scenario.IsWorking ? 500 : 440);
            cancelWork.interactable = scenario.CanCancelWork;
            if (scenario.IsWorking)
            {
                actor.text = scenario.LastActor + " 님이 ‘" + definition.Steps[completed].Title + "’ 작업 중";
                actor.resizeTextForBestFit = true;
                actor.resizeTextMinSize = 10;
                actor.resizeTextMaxSize = 12;
                float amount = scenario.WorkProgress;
                workStatus.text = "작업 진행  " + Mathf.RoundToInt(amount * 100) + "%";
                workFill.anchorMax = new Vector2(amount, 1);
            }

            for (int index = 0; index < rows.Count; index++)
            {
                bool done = index < completed;
                bool current = scenario.IsReady && !scenario.IsComplete && index == completed;
                rows[index].Text.text = (done ? "완료 · " : current ? "진행 · " : "대기 · ") + definition.Steps[index].Title;
                rows[index].Text.color = done ? Success : current ? Color.white : Muted;
                rows[index].Background.color = current ? new Color32(30, 58, 95, 255) : new Color32(15, 23, 42, 0);
            }

            if (displayedStep != completed)
            {
                displayedStep = completed;
                Canvas.ForceUpdateCanvases();
                stepScroll.verticalNormalizedPosition = rows.Count <= 2 ? 1 : 1 - Mathf.Clamp01(completed / (float)(rows.Count - 1));
            }
        }


        private void RebuildSteps(ScenarioDefinition current)
        {
            foreach (var row in rows)
            {
                row.Root.SetActive(false);
                Object.Destroy(row.Root);
            }

            rows.Clear();
            definition = current;
            displayedStep = -1;
            foreach (var step in current.Steps)
            {
                var background = Box("Scenario Step " + step.Id, stepContent, Color.clear);
                background.gameObject.AddComponent<LayoutElement>().preferredHeight = 28;
                var text = Label("Step Title", background.transform, 14);
                Stretch(text.rectTransform, 8, 0);
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 11;
                text.resizeTextMaxSize = 14;
                rows.Add((background.gameObject, background, text));
            }
        }


        private Text CreateText(string name, string value, int size, float top, float height)
        {
            var text = Label(name, root.transform, size);
            Place(text.rectTransform, 20, top, 336, height);
            text.text = value;
            return text;
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
            image.raycastTarget = false;
            return image;
        }


        private static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }


        private static void Stretch(RectTransform rect, float horizontal, float vertical)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
        }
    }
}
