// Unity를 실행하지 않고 연결 제어 로직을 검사하기 위한 최소 대체 타입입니다. 실제 Unity JSON·소켓 연동 검사는 별도로 수행합니다.
namespace UnityEngine
{
    public static class Time { public static float realtimeSinceStartup => 0; }
    public static class JsonUtility
    {
        public static string ToJson(object value) => System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
        public static T FromJson<T>(string value) => System.Text.Json.JsonSerializer.Deserialize<T>(value, new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
    }
}
namespace SmartTraining
{
    public class RoomHubResult { public bool success; }
}
