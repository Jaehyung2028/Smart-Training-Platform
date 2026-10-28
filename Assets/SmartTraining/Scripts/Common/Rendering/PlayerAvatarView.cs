using System;
using UnityEngine;

namespace SmartTraining
{
    // 참가자 정보나 씬을 모르는 기본 아바타 표시입니다. 표시 여부·로컬 시점 여부를 전달받습니다.
    public sealed class PlayerAvatarView : IDisposable
    {
        private readonly Transform parent;
        private GameObject visual;
        private Material material;

        public PlayerAvatarView(Transform parent)
        {
            this.parent = parent;
        }


        public bool HasVisual => visual != null;


        public void Show(bool localFirstPerson)
        {
            if (visual != null)
            {
                return;
            }

            visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "PlayerVisual";
            visual.transform.SetParent(parent, false);
            var collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                return;
            }

            material = new Material(shader)
            {
                color = localFirstPerson ? new Color(0.2f, 0.85f, 0.45f) : new Color(0.25f, 0.6f, 1f)
            };
            var renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.enabled = !localFirstPerson;
        }


        public void Hide()
        {
            if (visual != null)
            {
                visual.SetActive(false);
                UnityEngine.Object.Destroy(visual);
                visual = null;
            }

            if (material != null)
            {
                UnityEngine.Object.Destroy(material);
                material = null;
            }
        }


        public void Dispose()
        {
            Hide();
        }
    }
}
