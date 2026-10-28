using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartTraining
{
    // 단계 하나는 필요한 공구와 작업 대상의 조합입니다. 공구 선택은 별도 단계가 아닙니다.
    // 진행 상태가 아닌 콘텐츠 원본입니다. 참가자별 상태를 이 에셋에 저장하지 않습니다.
    [CreateAssetMenu(menuName = "Smart Training/Scenario Definition", fileName = "ScenarioDefinition")]
    public sealed class ScenarioDefinition : ScriptableObject
    {
        [Serializable]
        public sealed class Tool
        {
            [SerializeField]
            private string id = "";
            [SerializeField]
            private string title = "";

            public string Id => id;

            public string Title => title;
        }


        [Serializable]
        public sealed class Step
        {
            [SerializeField, Tooltip("단계의 고유 ID입니다. 제목이 바뀌어도 유지합니다.")]
            private string id = "";
            [SerializeField]
            private string title = "";
            [SerializeField, TextArea(2, 5)]
            private string instruction = "";
            [SerializeField, Tooltip("필요한 공구의 ID입니다. 비워 두면 공구 없이 대상 클릭만으로 완료합니다.")]
            private string toolId = "";
            [SerializeField, Tooltip("작업할 대상의 ScenarioObject ID입니다. 공구가 없는 목표도 필수입니다.")]
            private string targetId = "";

            public string Id => id;

            public string Title => title;

            public string Instruction => instruction;

            public string ToolId => toolId;

            public bool RequiresTool => !string.IsNullOrWhiteSpace(toolId);

            public string TargetId => targetId;
        }


        [SerializeField]
        private string id = "";
        [SerializeField]
        private string title = "";
        [SerializeField]
        private Step[] steps = Array.Empty<Step>();
        [SerializeField, Tooltip("메뉴에서 선택할 공구 목록입니다. 정답 공구뿐 아니라 오답 공구도 등록합니다.")]
        private Tool[] tools = Array.Empty<Tool>();

        public string Id => id;

        public string Title => title;

        public IReadOnlyList<Step> Steps => steps ?? Array.Empty<Step>();

        public IReadOnlyList<Tool> Tools => tools ?? Array.Empty<Tool>();


        public bool HasTool(string toolId)
        {
            foreach (var tool in Tools)
            {
                if (tool != null && tool.Id == toolId)
                {
                    return true;
                }
            }

            return false;
        }


        public string GetToolTitle(string toolId)
        {
            foreach (var tool in Tools)
            {
                if (tool != null && tool.Id == toolId)
                {
                    return tool.Title;
                }
            }

            return "없음";
        }


        public bool TryValidate(out string message)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title) || Steps.Count == 0)
            {
                message = "시나리오 ID·제목·단계를 지정해주세요.";
                return false;
            }

            var toolIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tool in Tools)
            {
                if (tool == null ||
                    string.IsNullOrWhiteSpace(tool.Id) ||
                    string.IsNullOrWhiteSpace(tool.Title) ||
                    System.Text.Encoding.UTF8.GetByteCount(tool.Id) > 60 ||
                    !toolIds.Add(tool.Id))
                {
                    message = "메뉴 공구에 중복되지 않는 ID와 표시 이름을 지정해주세요.";
                    return false;
                }
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in Steps)
            {
                if (step == null ||
                    string.IsNullOrWhiteSpace(step.Id) ||
                    !ids.Add(step.Id) ||
                    string.IsNullOrWhiteSpace(step.Title) ||
                    string.IsNullOrWhiteSpace(step.Instruction) ||
                    string.IsNullOrWhiteSpace(step.TargetId) ||
                    (step.RequiresTool &&
                    !toolIds.Contains(step.ToolId)))
                {
                    message = "각 단계에 중복되지 않는 ID·제목·안내·대상 ID를 지정하고, 필요한 공구는 Tools에 등록해주세요.";
                    return false;
                }
            }

            message = "";
            return true;
        }
    }
}
