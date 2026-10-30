using System;
using UnityEngine;

namespace SmartTraining
{
    // 콘텐츠 씬에 미리 배치한 로컬 표시 요소입니다. 선택 콘텐츠만 리소스 기준점 아래에 불러옵니다.
    [DisallowMultipleComponent]
    public sealed class ContentSceneLayout : MonoBehaviour
    {
        [SerializeField]
        private FirstPersonCameraController playerCamera;
        [SerializeField]
        private UiPrefabReferences hud;
        [SerializeField]
        private ContentResourceContext resources;

        public FirstPersonCameraController PlayerCamera => playerCamera;

        public UiPrefabReferences Hud => hud;

        public ContentResourceContext Resources => resources;


        public void ValidateReferences()
        {
            if (playerCamera == null || hud == null || resources == null)
            {
                throw new InvalidOperationException("TrainingContent 씬의 카메라·HUD·콘텐츠 기준점을 연결해주세요.");
            }
        }
    }
}
