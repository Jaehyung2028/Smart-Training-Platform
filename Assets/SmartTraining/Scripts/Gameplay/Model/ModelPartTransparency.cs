using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmartTraining
{
    // 선택하지 않은 부품의 로컬 투명 표시입니다. 원본 재질 에셋은 변경하지 않습니다.
    public sealed class ModelPartTransparency : IDisposable
    {
        private sealed class State
        {
            public Material[] Originals, Copies;
            public readonly List<MaterialPropertyBlock> Blocks = new();
        }

        private readonly Dictionary<Renderer, State> states = new();

        // 강조를 해제한 뒤 호출합니다. 원래 슬롯 색상·사용자 PropertyBlock도 함께 보존합니다.
        public void Apply(Renderer renderer, float opacity)
        {
            var state = new State { Originals = renderer.sharedMaterials };
            state.Copies = new Material[state.Originals.Length];
            var materials = (Material[])state.Originals.Clone();
            for (int i = 0; i < materials.Length; i++)
            {
                var originalBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(originalBlock, i);
                state.Blocks.Add(originalBlock);
                var source = materials[i];
                // 현재 프로젝트의 URP Lit/Unlit 및 Standard 재질을 지원합니다. 사용자 셰이더는 그대로 둡니다.
                if (source == null || !(source.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal) ||
                    source.shader.name == "Standard" || source.shader.name == "Standard (Specular setup)"))
                {
                    continue;
                }
                string colorName = source.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                if (!source.HasProperty(colorName))
                {
                    continue;
                }
                var copy = new Material(source) { name = source.name + " (Local Transparency)", hideFlags = HideFlags.DontSave };
                state.Copies[i] = copy;
                materials[i] = copy;
                SetFloat(copy, "_Surface", 1);
                SetFloat(copy, "_Mode", 2);
                SetFloat(copy, "_Blend", 0);
                SetFloat(copy, "_AlphaClip", 0);
                SetFloat(copy, "_SrcBlend", (float)BlendMode.SrcAlpha);
                SetFloat(copy, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                SetFloat(copy, "_SrcBlendAlpha", (float)BlendMode.One);
                SetFloat(copy, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                SetFloat(copy, "_ZWrite", 0);
                copy.SetOverrideTag("RenderType", "Transparent");
                copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                copy.EnableKeyword("_ALPHABLEND_ON");
                copy.DisableKeyword("_ALPHATEST_ON");
                copy.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                copy.DisableKeyword("_ALPHAMODULATE_ON");
                copy.SetShaderPassEnabled("ShadowCaster", false);
                copy.SetShaderPassEnabled("DepthOnly", false);
                copy.SetShaderPassEnabled("DepthNormals", false);
                copy.renderQueue = (int)RenderQueue.Transparent;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block, i);
                if (block.isEmpty)
                {
                    renderer.GetPropertyBlock(block);
                }
                int colorId = Shader.PropertyToID(colorName);
                var color = block.HasColor(colorId) ? block.GetColor(colorId) : source.GetColor(colorId);
                color.a *= opacity;
                copy.SetColor(colorId, color);
                block.SetColor(colorId, color);
                renderer.SetPropertyBlock(block, i);
            }
            renderer.sharedMaterials = materials;
            states.Add(renderer, state);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        // 선택 변경·기능 해제·콘텐츠 종료 시 원래 재질과 슬롯 설정을 되돌리고 임시 재질을 해제합니다.
        public void Clear()
        {
            foreach (var pair in states)
            {
                if (pair.Key != null)
                {
                    pair.Key.sharedMaterials = pair.Value.Originals;
                    for (int i = 0; i < pair.Value.Blocks.Count; i++)
                    {
                        var block = pair.Value.Blocks[i];
                        pair.Key.SetPropertyBlock(block.isEmpty ? null : block, i);
                    }
                }
                foreach (var material in pair.Value.Copies)
                {
                    if (material == null)
                    {
                        continue;
                    }
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(material);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(material);
                    }
                }
            }
            states.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
