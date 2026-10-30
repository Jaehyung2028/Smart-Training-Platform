using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // 목표·진행·공구 조건과 작업 취소 버튼을 표시합니다. 권한·작업 판정은 서버에 맡깁니다.
    internal sealed class ScenarioControlPanel
    {
        private readonly GameObject root;
        private readonly Vector2 preferredSize;
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
            var view = UiPrefab.Bind("ScenarioControlPanel", parent);
            root = view.Get<GameObject>("root");
            preferredSize = ((RectTransform)root.transform).sizeDelta;
            title = view.Get<Text>("title");
            progress = view.Get<Text>("progress");
            currentTitle = view.Get<Text>("currentTitle");
            instruction = view.Get<Text>("instruction");
            requirement = view.Get<Text>("requirement");
            myTool = view.Get<Text>("myTool");
            actor = view.Get<Text>("actor");
            requirementBackground = view.Get<Image>("requirementBackground");
            progressFill = view.Get<RectTransform>("progressFill");
            workRoot = view.Get<GameObject>("workRoot");
            workStatus = view.Get<Text>("workStatus");
            workFill = view.Get<RectTransform>("workFill");
            cancelWork = view.Get<Button>("cancelWork");
            stepScroll = view.Get<ScrollRect>("stepScroll");
            stepContent = view.Get<RectTransform>("stepContent");
            feedbackRoot = view.Get<GameObject>("feedbackRoot");
            feedback = view.Get<Text>("feedback");
            cancelWork.onClick.AddListener(() =>
            {
                if (cancelWork.IsInteractable())
                {
                    previous?.CancelWork();
                }
            });
            root.SetActive(false);
            feedbackRoot.SetActive(false);
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
                requirement.text = step.RequiresTool ? "필요 공구  ·  " + definition.GetToolTitle(step.ToolId) : "공구 없이 진행  ·  선택 해제 후 대상 클릭";
                requirement.color = step.RequiresTool ? Accent : Success;
                if (definition.RequiresRoles)
                {
                    requirement.text = ScenarioRoles.Label(step.RequiredRole) + " · " + (step.RequiresTool ? definition.GetToolTitle(step.ToolId) : "공구 불필요");
                    if ((scenario.SelectedRoles & step.RequiredRole) != step.RequiredRole)
                    {
                        instruction.text += "\n담당자는 역할 메뉴에서 확인할 수 있습니다.";
                    }
                }

                myTool.text = step.RequiresTool ? "내 공구: " + definition.GetToolTitle(scenario.SelectedTool) : "공구 선택을 해제한 뒤 대상을 클릭해주세요.";
                if (definition.RequiresRoles)
                {
                    myTool.text = "내 역할: " + ScenarioRoles.Label(scenario.SelectedRoles) + " · 공구: " + definition.GetToolTitle(scenario.SelectedTool);
                }

                myTool.color = scenario.SelectedTool != step.ToolId ? new Color32(251, 191, 36, 255) : Muted;
            }

            if (scenario.IsWorking)
            {
                requirement.text = scenario.CanCancelWork ? "내 작업 진행 중  ·  취소 가능" : "다른 참가자 작업 중";
                requirement.color = Accent;
            }

            currentTitle.color = scenario.IsComplete ? Success : Color.white;
            actor.text = scenario.LastActor.Length > 0 ? "최근 진행  ·  " + scenario.LastActor : "진행 결과는 참가자 모두에게 공유됩니다.";
            workRoot.SetActive(scenario.IsWorking);
            root.GetComponent<RectTransform>().sizeDelta = preferredSize + Vector2.up * (scenario.IsWorking ? 60 : 0);
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
                UiPrefab.Destroy(row.Root);
            }

            rows.Clear();
            definition = current;
            displayedStep = -1;
            foreach (var step in current.Steps)
            {
                var prefab = UiPrefab.Instantiate("ScenarioStepRow", stepContent);
                prefab.name = "Scenario Step " + step.Id;
                var background = prefab.GetComponent<Image>();
                var text = prefab.Get<Text>("Step Title:Text");
                rows.Add((background.gameObject, background, text));
            }
        }
    }
}
