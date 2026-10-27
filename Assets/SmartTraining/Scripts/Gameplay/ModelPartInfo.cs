using UnityEngine;

namespace SmartTraining
{
    // 설명할 부품 또는 여러 Renderer를 묶는 부모에 붙입니다. 설명은 개인 화면에서 읽으며 네트워크 상태가 아닙니다.
    public sealed class ModelPartInfo : MonoBehaviour
    {
        [SerializeField, Tooltip("설명 창에 표시할 부품 이름입니다.")]
        private string title;

        [SerializeField, TextArea(3, 10), Tooltip("부품의 역할과 관찰할 내용을 입력합니다.")]
        private string description;

        public string Title => string.IsNullOrWhiteSpace(title) ? gameObject.name : title;
        public string Description => string.IsNullOrWhiteSpace(description) ? "등록된 부품 설명이 없습니다." : description;
    }
}
