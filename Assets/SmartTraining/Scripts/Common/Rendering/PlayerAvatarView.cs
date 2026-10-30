using System;
using UnityEngine;

namespace SmartTraining
{
    // 모델·Animator·표시 정보만 전달받습니다. 이동 권한이나 네트워크·콘텐츠 상태는 조회하지 않습니다.
    public sealed class PlayerAvatarView : IDisposable
    {
        private static readonly int Speed = Animator.StringToHash("Speed");
        private readonly Transform parent;
        private readonly GameObject visual;
        private readonly Animator animator;
        private readonly WorldNameTagReferences nameTagPrefab;
        private WorldNameTagView nameTag;
        private Vector3 previousPosition;
        private bool shown;

        public PlayerAvatarView(
            Transform parent,
            Transform visualRoot,
            Animator animator,
            WorldNameTagReferences nameTagPrefab = null
        )
        {
            this.parent = parent;
            visual = visualRoot != null ? visualRoot.gameObject : null;
            this.animator = animator;
            this.nameTagPrefab = nameTagPrefab;
        }


        public bool HasVisual => shown;


        public void Show(bool localFirstPerson)
        {
            if (shown || visual == null)
            {
                return;
            }

            shown = true;
            previousPosition = parent.position;
            // 1인칭에서는 자신의 전신이 카메라와 시야를 가리지 않게 합니다.
            visual.SetActive(!localFirstPerson);
            if (!localFirstPerson && nameTagPrefab != null)
            {
                nameTag ??= new WorldNameTagView(parent, nameTagPrefab);
            }
        }


        public void Refresh(string displayName, Camera viewCamera, string detail = "", float deltaTime = 0)
        {
            if (!shown)
            {
                return;
            }

            nameTag?.Refresh(displayName, viewCamera, detail);
            float distance = Vector3.ProjectOnPlane(parent.position - previousPosition, Vector3.up).magnitude;
            previousPosition = parent.position;
            if (animator != null && animator.isActiveAndEnabled && animator.isInitialized && deltaTime > 0)
            {
                // 동기화된 실제 이동을 사용하므로 애니메이션용 RPC를 매 프레임 전송하지 않습니다.
                // 시작 위치 초기화처럼 큰 좌표 보정은 달리기로 표시하지 않습니다.
                float speed = distance < 2 ? Mathf.Min(distance / deltaTime, 6) : 0;
                animator.SetFloat(Speed, speed < .08f ? 0 : speed, .12f, deltaTime);
            }
        }


        public void Hide()
        {
            shown = false;
            if (visual != null)
            {
                visual.SetActive(false);
            }

            nameTag?.Refresh("", null);
        }


        public void Dispose()
        {
            Hide();
            nameTag?.Dispose();
            nameTag = null;
        // 모델은 프리팹 소유 자식이므로 표시 모듈에서 생성하거나 파괴하지 않습니다.
        }
    }
}
