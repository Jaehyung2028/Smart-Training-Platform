using UnityEngine;

namespace SmartTraining
{
    public enum ScenarioObjectKind
    {
        Tool,
        Target
    }

    // 오브젝트 이름·배치와 시나리오의 연결을 분리합니다. 실제 조작 권한과 진행 상태는 담지 않습니다.
    [DisallowMultipleComponent]
    public sealed class ScenarioObject : MonoBehaviour
    {
        [SerializeField]
        private string id = "";
        [SerializeField]
        private ScenarioObjectKind kind;

        public string Id => id;
        public ScenarioObjectKind Kind => kind;
    }
}
