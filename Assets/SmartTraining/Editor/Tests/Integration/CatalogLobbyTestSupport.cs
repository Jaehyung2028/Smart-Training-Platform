using System;
using System.Collections;
using System.Linq;

namespace SmartTraining.Editor
{
    // 예전 수동 생성 대신 운영 서버의 고정 카탈로그에서 격리해 사용할 빈 로비를 고릅니다.
    internal static class CatalogLobbyTestSupport
    {
        public static IEnumerator Select(TrainingApiClient api, Action<string> success, Action<string> failure, string excludedLobbyId = null)
        {
            yield return api.GetLobbies(items =>
            {
                var selected = items?.Where(item => item.id != excludedLobbyId && !item.isMaintenance &&
                    item.shutdownState != "scheduled" && item.shutdownState != "completed" && item.userCount == 0 && item.roomCount == 0)
                    .OrderByDescending(item => item.id == "sample-training").ThenBy(item => item.id, StringComparer.Ordinal).FirstOrDefault();
                if (selected == null)
                    throw new InvalidOperationException("검사에 사용할 빈 카탈로그 로비가 없습니다. 로비 목록과 기존 접속자를 확인해주세요.");
                success(selected.id);
            }, failure);
        }
    }
}
