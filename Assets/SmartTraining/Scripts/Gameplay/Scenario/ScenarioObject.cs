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
        [SerializeField, Tooltip("완전히 풀었을 때의 이동량입니다. 부모의 로컬 좌표를 사용합니다.")]
        private Vector3 loosenOffset = new Vector3(0, .18f, 0);
        [SerializeField, Range(0, 8)]
        private float loosenTurns = 2;
        [SerializeField, Tooltip("공구가 맞닿을 위치·방향입니다. 비워 두면 이 오브젝트의 원점을 사용합니다.")]
        private Transform workAnchor;

        public Transform WorkAnchor => workAnchor != null ? workAnchor : transform;

        public Vector3 LoosenOffset => loosenOffset;

        public float LoosenTurns => loosenTurns;

        public string Id => id;

        public ScenarioObjectKind Kind => kind;
    }
}
