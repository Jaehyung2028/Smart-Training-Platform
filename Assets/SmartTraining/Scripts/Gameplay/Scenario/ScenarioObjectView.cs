using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartTraining
{
    // 오브젝트 ID·진행 규칙과 별개로 로컬 색상과 체결 모습을 표현합니다. 원본 재질은 수정하지 않습니다.
    internal sealed class ScenarioObjectView : IDisposable
    {
        private readonly ScenarioObject item;
        private readonly Vector3 initialPosition;
        private readonly Quaternion initialRotation;
        private readonly Renderer[] renderers;
        private readonly List<MaterialPropertyBlock[]> originals = new();
        private int lastState = -1;

        public ScenarioObjectView(ScenarioObject item)
        {
            this.item = item;
            initialPosition = item.transform.localPosition;
            initialRotation = item.transform.localRotation;
            renderers = item.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                var saved = new MaterialPropertyBlock[renderer.sharedMaterials.Length];
                for (int index = 0; index < saved.Length; index++)
                {
                    saved[index] = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(saved[index], index);
                }

                originals.Add(saved);
            }
        }


        public void Refresh(bool active, int completedUses, bool completed)
        {
            int visualState = active ? 1 : completed ? 2 : 0;
            if (lastState != visualState)
            {
                lastState = visualState;
                Color color = visualState == 1 ? new Color32(251, 191, 36, 255) : new Color32(74, 222, 128, 255);
                for (int r = 0; r < renderers.Length; r++)
                {
                    var renderer = renderers[r];
                    if (renderer == null)
                    {
                        continue;
                    }

                    var materials = renderer.sharedMaterials;
                    for (int index = 0; index < materials.Length; index++)
                    {
                        var block = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(block, index);
                        if (visualState == 0)
                        {
                            renderer.SetPropertyBlock(originals[r][index], index);
                            continue;
                        }

                        if (materials[index] != null)
                        {
                            if (materials[index].HasProperty("_BaseColor"))
                            {
                                block.SetColor("_BaseColor", color);
                            }
                            else if (materials[index].HasProperty("_Color"))
                            {
                                block.SetColor("_Color", color);
                            }
                        }

                        renderer.SetPropertyBlock(block, index);
                    }
                }
            }

            // 예시 볼트 풀기 연출입니다. 진행 판정은 이 Transform이 아닌 서버 단계 값으로 합니다.
            float blend = 1 - Mathf.Exp(-10 * Time.unscaledDeltaTime);
            var targetPosition = initialPosition + Vector3.up * (0.04f * completedUses);
            var targetRotation = initialRotation * Quaternion.Euler(0, -90 * completedUses, 0);
            item.transform.localPosition = Vector3.Lerp(item.transform.localPosition, targetPosition, blend);
            item.transform.localRotation = Quaternion.Slerp(item.transform.localRotation, targetRotation, blend);
        }


        public void Dispose()
        {
            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] == null)
                {
                    continue;
                }

                for (int index = 0; index < originals[r].Length; index++)
                {
                    renderers[r].SetPropertyBlock(originals[r][index], index);
                }
            }

            if (item != null)
            {
                item.transform.localPosition = initialPosition;
                item.transform.localRotation = initialRotation;
            }
        }
    }
}
