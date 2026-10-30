using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SmartTraining.Editor
{
    public static class RuntimeOptimizationChecks
    {
        [MenuItem("Smart Training/Content/Check Runtime Optimizations")]
        public static void Run()
        {
            var root = new GameObject("Optimization Check", typeof(RectTransform), typeof(Canvas));
            ChatMessageListView view = null;
            try
            {
                var content = new GameObject(
                    "Messages",
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup),
                    typeof(ContentSizeFitter)
                );
                content.transform.SetParent(root.transform, false);
                var scroll = root.AddComponent<ScrollRect>();
                scroll.viewport = root.GetComponent<RectTransform>();
                scroll.content = content.GetComponent<RectTransform>();
                int created = 0;
                view = new ChatMessageListView(scroll, () =>
                {
                    created++;
                    var row = new GameObject("Check Message", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                    row.transform.SetParent(content.transform, false);
                    return row;
                }, Color.white);
                var messages = new List<LobbyChatMessage>();
                for (int index = 0; index < 500; index++)
                {
                    if (messages.Count == 100)
                    {
                        messages.RemoveAt(0);
                    }

                    messages.Add(new LobbyChatMessage { id = index.ToString(), userId = "self", nickname = "사용자", text = "메시지 " + index, sentAt = "2026-10-29T00:00:00Z" });
                    view.Refresh(messages, "self", "room-a");
                }

                Require(created == 100, "500개 순환 메시지를 표시해도 100개 행만 생성");
                Require(content.GetComponentsInChildren<Text>().Length == 100, "현재 이력만 활성 상태");
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int repeat = 0; repeat < 1000; repeat++)
                {
                    view.Refresh(messages, "self", "room-a");
                }

                long steadyBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Require(created == 100, "변경 없는 목록에서 새 행 생성 없음");
                view.Clear();
                Require(content.GetComponentsInChildren<Text>().Length == 0, "반환된 행 비활성화");
                Require(
                    content.GetComponentsInChildren<Text>(true).All(row => row.text == ""),
                    "반환 시 이전 대화 제거"
                );
                messages.RemoveRange(2, messages.Count - 2);
                view.Refresh(messages, "other", "room-b");
                Require(
                    created == 100 && content.GetComponentsInChildren<Text>().Length == 2,
                    "새 방에서 풀 재사용"
                );
                Require(
                    content.GetComponentsInChildren<Text>().All(row => row.color == Color.white),
                    "이전 본인 색상 초기화"
                );
                view.Refresh(messages, "self", "room-b");
                Require(
                    content.GetComponentsInChildren<Text>().All(row => row.color == ChatMessagePresentation.OwnMessageColor),
                    "사용자 변경 시 본인 색상 갱신"
                );
                UiPrefab.Instantiate("ContentParticipantPanel", root.transform);
                var panel = new ContentParticipantPanel(root.transform, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
                panel.Toggle();
                var participants = new[]
                {
                    new ContentParticipantState("a", "참가자", true, false, "정상 연결")
                };
                panel.Refresh(participants, true);
                participants[0] = new ContentParticipantState("a", "참가자", true, true, "연결 확인 중");
                panel.Refresh(participants, true);
                var participantRow = root.GetComponentsInChildren<Text>().Single(row => row.name == "Content Participant Row");
                Require(
                    participantRow.text.Contains("[방장]") && participantRow.text.Contains("연결 확인 중"),
                    "재사용 배열에서도 상태 변경 감지"
                );
                var mask = ScenarioRole.Inspector | ScenarioRole.Technician | ScenarioRole.SafetyOfficer | ScenarioRole.Coordinator;
                string label = ScenarioRoles.Label(mask);
                ScenarioRoles.Label(mask, "\n");
                before = GC.GetAllocatedBytesForCurrentThread();
                for (int repeat = 0; repeat < 10000; repeat++)
                {
                    ScenarioRoles.Label(mask);
                    ScenarioRoles.Label(mask, "\n");
                }

                long labelBytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Require(
                    labelBytes == 0 && ReferenceEquals(label, ScenarioRoles.Label(mask)),
                    "역할 문자열 캐시는 추가 할당 없음"
                );
                Debug.Log("RUNTIME OPTIMIZATION CHECKS PASSED: rows=" + created + "; 1000 steady chat refresh bytes=" + steadyBytes + "; 20000 role label calls bytes=" + labelBytes);
            }
            finally
            {
                view?.Clear();
                Object.DestroyImmediate(root);
                view?.Dispose();
            }
        }


        private static void Require(bool success, string message)
        {
            if (!success)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
