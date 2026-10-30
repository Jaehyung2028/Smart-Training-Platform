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
        private readonly Vector2 preferredSize;
        private readonly RectTransform card, content;
        private readonly Text heading, summary, hint;
        private readonly Button restart, returnToWaiting;
        private readonly List<GameObject> rows = new();
        private readonly Action returnAction;
        private ScenarioContent scenario;
        private int displayedRun = -1, displayedRows = -1;
        private bool canUse, canReturn;

        public ScenarioResultPanel(Transform parent, Font font, Action returnAction)
        {
            var view = UiPrefab.Bind("ScenarioResultPanel", parent);
            root = view.Get<GameObject>("root");
            card = view.Get<RectTransform>("card");
            preferredSize = card.sizeDelta;
            content = view.Get<RectTransform>("content");
            heading = view.Get<Text>("heading");
            summary = view.Get<Text>("summary");
            hint = view.Get<Text>("hint");
            restart = view.Get<Button>("restart");
            returnToWaiting = view.Get<Button>("returnToWaiting");
            this.returnAction = returnAction;
            restart.onClick.AddListener(() =>
            {
                if (canUse && scenario != null && scenario.IsComplete && scenario.CanReset)
                {
                    scenario.ResetPractice();
                }
            });
            returnToWaiting.onClick.AddListener(() =>
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

            card.sizeDelta = new Vector2(Mathf.Min(preferredSize.x, size.x - 32), Mathf.Min(preferredSize.y, size.y - 88));
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
                UiPrefab.Destroy(row);
            }

            rows.Clear();
            foreach (var outcome in outcomes)
            {
                var prefab = UiPrefab.Instantiate("ScenarioResultRow", content);
                var row = prefab.gameObject;
                row.name = "Scenario Result Row " + outcome.Index;
                prefab.Get<Text>("Result Step:Text").text = (outcome.Index + 1) + ". " + scenario.Definition.Steps[outcome.Index].Title;
                prefab.Get<Text>("Result Actor:Text").text = outcome.ActorName + " · " + Duration(outcome.DurationSeconds);
                prefab.Get<Text>("Result Errors:Text").text = "공구 " + outcome.ToolErrors + " · 대상 " + outcome.TargetErrors;
                rows.Add(row);
            }
        }


        private static string Duration(double seconds)
        {
            var duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return $"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}";
        }
    }
}
