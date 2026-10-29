using System;
using System.Collections.Generic;

namespace SmartTraining
{
    // 단계에는 단일 역할, 참가자의 보유 상태에는 여러 역할을 조합해 사용합니다.
    [Flags]
    public enum ScenarioRole
    {
        None = 0,
        Inspector = 1,
        Technician = 2,
        SafetyOfficer = 4,
        Coordinator = 8
    }


    public static class ScenarioRoles
    {
        private static readonly ScenarioRole[] individualRoles =
        {
            ScenarioRole.Inspector,
            ScenarioRole.Technician,
            ScenarioRole.SafetyOfficer,
            ScenarioRole.Coordinator
        };
        private static readonly string[] inlineLabels = BuildLabels(" · ");
        private static readonly string[] multilineLabels = BuildLabels("\n");

        private static string[] BuildLabels(string separator)
        {
            var result = new string[16];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = BuildLabel((ScenarioRole)index, separator);
            }

            return result;
        }


        public static IEnumerable<ScenarioRole> Enumerate(ScenarioRole roles)
        {
            foreach (ScenarioRole role in individualRoles)
            {
                if (role != ScenarioRole.None && (roles & role) == role)
                {
                    yield return role;
                }
            }
        }


        // 4개 역할의 16가지 조합을 재사용해 HUD·이름표의 매 프레임 문자열 할당을 줄입니다.
        public static string Label(ScenarioRole roles, string separator = " · ")
        {
            int index = (int)roles & 15;
            return separator == " · " ? inlineLabels[index] : separator == "\n" ? multilineLabels[index] : BuildLabel(roles, separator);
        }


        private static string BuildLabel(ScenarioRole roles, string separator)
        {
            var labels = new List<string>();
            foreach (var role in Enumerate(roles))
            {
                labels.Add(role switch
                {
                    ScenarioRole.Inspector => "점검 담당",
                    ScenarioRole.Technician => "정비 담당",
                    ScenarioRole.SafetyOfficer => "안전 확인 담당",
                    ScenarioRole.Coordinator => "진행 담당",
                    _ => "미선택"
                });
            }

            return labels.Count > 0 ? string.Join(separator, labels) : "미선택";
        }
    }
}
