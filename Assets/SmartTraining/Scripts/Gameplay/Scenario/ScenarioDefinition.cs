using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartTraining
{
    public enum ScenarioAction
    {
        Check,
        Loosen,
        Tighten,
        Strike,
        Drill,
        Grip,
        Saw,
        Chop
    }


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
            [SerializeField, Tooltip("손에 표시할 일반 모델 프리팹입니다. NetworkObject 없이 손의 Grip 기준으로 배치합니다.")]
            private GameObject heldPrefab;
            [SerializeField, Tooltip("작업 대상에 손과 공구를 가져가는 동작을 사용할지 정합니다.")]
            private bool animateWork;
            [SerializeField, Tooltip("공구 모델의 로컬 좌표로 작성한 실제 접촉점입니다.")]
            private Vector3 workContactPoint;
            [SerializeField, Tooltip("대상의 Work Anchor를 기준으로 한 공구의 작업 방향입니다.")]
            private Vector3 workRotation;

            public bool AnimateWork => animateWork;

            public Vector3 WorkContactPoint => workContactPoint;

            public Quaternion WorkRotation => Quaternion.Euler(workRotation);

            public GameObject HeldPrefab => heldPrefab;

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
            [SerializeField, Tooltip("확인은 즉시 완료하고, 다른 작업은 설정한 시간 동안 공구별 모션을 재생합니다.")]
            private ScenarioAction action;
            [SerializeField, Range(0.25f, 10)]
            private float durationSeconds = 1.5f;
            [SerializeField, Tooltip("협동 실습의 담당 역할입니다. None이면 기존 개인·공동 실습처럼 누구나 수행합니다.")]
            private ScenarioRole requiredRole;

            public ScenarioRole RequiredRole => requiredRole;

            public ScenarioAction Action => action;

            public float DurationSeconds => action == ScenarioAction.Check ? 0 : durationSeconds;

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


        public ScenarioRole Roles
        {
            get
            {
                var roles = ScenarioRole.None;
                foreach (var step in Steps)
                {
                    if (step != null)
                    {
                        roles |= step.RequiredRole;
                    }
                }

                return roles;
            }
        }


        public bool RequiresRoles => Roles != ScenarioRole.None;

        public string Title => title;

        public IReadOnlyList<Step> Steps => steps ?? Array.Empty<Step>();

        public IReadOnlyList<Tool> Tools => tools ?? Array.Empty<Tool>();


        public Tool GetTool(string toolId)
        {
            foreach (var tool in Tools)
            {
                if (tool != null && tool.Id == toolId)
                {
                    return tool;
                }
            }

            return null;
        }


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
                    !toolIds.Contains(step.ToolId)) ||
                    !Enum.IsDefined(typeof(ScenarioAction), step.Action) ||
                    !Enum.IsDefined(typeof(ScenarioRole), step.RequiredRole) ||
                    (step.Action != ScenarioAction.Check &&
                    (float.IsNaN(step.DurationSeconds) ||
                    float.IsInfinity(step.DurationSeconds) ||
                    step.DurationSeconds < .25f ||
                    step.DurationSeconds > 10)))
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
