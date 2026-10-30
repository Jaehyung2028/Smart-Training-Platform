using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // 프리팹에서 이름·역할 텍스트를 연결합니다. 자식 오브젝트 이름이나 순서가 바뀌어도 참조를 유지합니다.
    [DisallowMultipleComponent]
    public sealed class WorldNameTagReferences : MonoBehaviour
    {
        [SerializeField]
        private Canvas canvas;
        [SerializeField]
        private Text nameLabel;
        [SerializeField]
        private Text detailLabel;
        [SerializeField, Min(0), Tooltip("역할 한 줄마다 추가할 높이입니다. 자동 확장이 필요 없으면 0으로 설정합니다.")]
        private float detailLineHeight = 24;

        public Canvas Canvas => canvas;
        public Text NameLabel => nameLabel;
        public Text DetailLabel => detailLabel;
        public float DetailLineHeight => detailLineHeight;
    }
}
