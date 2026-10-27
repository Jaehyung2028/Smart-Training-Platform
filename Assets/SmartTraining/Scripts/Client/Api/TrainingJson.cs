using System;
using System.Text.Json;

namespace SmartTraining
{
    // HTTP와 SignalR에서 같은 JSON 규칙을 사용합니다. Unity DTO의 public 필드도 직렬화합니다.
    public static class TrainingJson
    {
        public static JsonSerializerOptions Options
        {
            get;
        } = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        };

        public static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, Options);
        }

        public static T Deserialize<T>(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json, Options);
            }
            catch (JsonException error)
            {
                // 기존 API의 형식 오류 처리와 연결합니다. JSON 구문 처리는 라이브러리에 맡깁니다.
                throw new FormatException("서버 JSON 응답 형식이 올바르지 않습니다.", error);
            }
        }
    }
}
