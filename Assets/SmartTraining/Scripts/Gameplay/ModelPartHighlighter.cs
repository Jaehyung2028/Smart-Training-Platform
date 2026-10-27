using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartTraining
{
    // 모델 부품의 로컬 강조 표시입니다. 재질 에셋·NetworkTransform·다른 참가자의 표시는 변경하지 않습니다.
    public sealed class ModelPartHighlighter : IDisposable
    {
        private sealed class Part
        {
            public Renderer Renderer;
            public Collider Collider;
            public bool OwnsCollider;
        }

        // 마우스 강조와 선택 강조가 각각 원래 슬롯 설정을 보관합니다. 같은 부품이면 선택 강조만 적용합니다.
        private sealed class TintState
        {
            private readonly Color color;
            private readonly List<MaterialPropertyBlock> savedBlocks = new();
            public Renderer Renderer { get; private set; }

            public TintState(Color color)
            {
                this.color = color;
            }

            public void Set(Renderer renderer)
            {
                if (Renderer == renderer)
                {
                    return;
                }
                Clear();
                Renderer = renderer;
                if (Renderer == null)
                {
                    return;
                }
                var materials = Renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var original = new MaterialPropertyBlock();
                    Renderer.GetPropertyBlock(original, i);
                    savedBlocks.Add(original);
                    var material = materials[i];
                    if (material == null || (!material.HasProperty(BaseColorId) && !material.HasProperty(ColorId)))
                    {
                        continue;
                    }
                    var block = new MaterialPropertyBlock();
                    if (original.isEmpty)
                    {
                        Renderer.GetPropertyBlock(block);
                    }
                    else
                    {
                        Renderer.GetPropertyBlock(block, i);
                    }
                    int property = material.HasProperty(BaseColorId) ? BaseColorId : ColorId;
                    var tint = color;
                    tint.a = material.GetColor(property).a;
                    block.SetColor(property, tint);
                    Renderer.SetPropertyBlock(block, i);
                }
            }

            public void Clear()
            {
                if (Renderer != null)
                {
                    for (int i = 0; i < savedBlocks.Count; i++)
                    {
                        Renderer.SetPropertyBlock(savedBlocks[i].isEmpty ? null : savedBlocks[i], i);
                    }
                }
                savedBlocks.Clear();
                Renderer = null;
            }
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private readonly List<Part> parts = new();
        private readonly TintState hoverTint = new(new Color(0.25f, 0.9f, 1f));
        private readonly Dictionary<Renderer, TintState> selectionTints = new();
        private readonly ModelPartTransparency transparency = new();
        private bool fadeSurroundings;
        private float surroundingsOpacity = 0.3f;
        private Renderer highlighted;
        private bool disposed;

        public Renderer Highlighted => highlighted;
        public int SelectedCount => selectionTints.Count;

        public bool IsSelected(Renderer renderer) => renderer != null && selectionTints.ContainsKey(renderer);

        public ModelPartHighlighter(IReadOnlyList<Renderer> renderers)
        {
            foreach (var renderer in renderers)
            {
                var collider = renderer.GetComponent<Collider>();
                bool ownsCollider = collider == null || !collider.enabled;
                if (ownsCollider)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (renderer is MeshRenderer && filter != null && filter.sharedMesh != null && filter.sharedMesh.isReadable)
                    {
                        // 경계 상자만 검사하면 드릴·원통의 빈 공간도 선택되므로 정적 메시의 실제 표면을 사용합니다.
                        var meshCollider = renderer.gameObject.AddComponent<MeshCollider>();
                        meshCollider.sharedMesh = filter.sharedMesh;
                        collider = meshCollider;
                    }
                    else
                    {
                        // 읽을 수 없는 메시·변형 모델에는 로컬 경계 상자를 사용합니다. 정밀 선택이 필요하면 프리팹에 Collider를 지정합니다.
                        var boxCollider = renderer.gameObject.AddComponent<BoxCollider>();
                        boxCollider.center = renderer.localBounds.center;
                        boxCollider.size = renderer.localBounds.size;
                        collider = boxCollider;
                    }
                }
                parts.Add(new Part { Renderer = renderer, Collider = collider, OwnsCollider = ownsCollider });
            }
        }

        public void UpdateHover(Ray ray, bool canHover, float maxDistance)
        {
            if (disposed)
            {
                return;
            }
            SetHighlighted(canHover ? FindPart(ray, maxDistance) : null);
        }

        // 클릭과 강조가 같은 표면 판정을 사용합니다. 선택만 조회할 때는 강조 색상을 바꾸지 않습니다.
        public Renderer FindPart(Ray ray, float maxDistance)
        {
            if (disposed)
            {
                return null;
            }
            Renderer nearest = null;
            float nearestDistance = maxDistance;
            Physics.SyncTransforms();
            foreach (var part in parts)
            {
                if (part.Renderer != null && part.Renderer.enabled && part.Renderer.gameObject.activeInHierarchy &&
                    part.Collider != null && part.Collider.enabled && part.Collider.Raycast(ray, out var hit, nearestDistance))
                {
                    nearestDistance = hit.distance;
                    nearest = part.Renderer;
                }
            }
            return nearest;
        }

        private void SetHighlighted(Renderer renderer)
        {
            highlighted = renderer;
            hoverTint.Set(!IsSelected(renderer) ? renderer : null);
        }

        // 마우스 강조를 먼저 복원한 뒤 선택 색상을 적용해 청록색이 원래 색상으로 저장되지 않도록 합니다.
        public void SetSelected(Renderer renderer)
        {
            SetSelection(renderer != null ? new[] { renderer } : Array.Empty<Renderer>());
        }

        public void SetSelection(IReadOnlyList<Renderer> renderers)
        {
            if (disposed)
            {
                return;
            }
            var desired = new HashSet<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    desired.Add(renderer);
                }
            }
            if (desired.SetEquals(selectionTints.Keys))
            {
                return;
            }
            RefreshTints(desired);
        }

        // 이전 강조를 복원한 다음 현재 선택을 기준으로 투명도와 주황색을 적용합니다.
        private void RefreshTints(HashSet<Renderer> desired)
        {
            hoverTint.Clear();
            foreach (var tint in selectionTints.Values)
            {
                tint.Clear();
            }
            selectionTints.Clear();
            transparency.Clear();
            if (fadeSurroundings && desired.Count > 0 && surroundingsOpacity < 1)
            {
                foreach (var part in parts)
                {
                    if (part.Renderer != null && !desired.Contains(part.Renderer))
                    {
                        transparency.Apply(part.Renderer, surroundingsOpacity);
                    }
                }
            }
            foreach (var renderer in desired)
            {
                var tint = new TintState(new Color(1f, 0.55f, 0.1f));
                tint.Set(renderer);
                selectionTints.Add(renderer, tint);
            }
            hoverTint.Set(!IsSelected(highlighted) ? highlighted : null);
        }

        // 투명도 변경도 선택 변경과 같은 복원 순서를 사용해 강조 색상을 원래 색상으로 저장하지 않습니다.
        public void SetSurroundingsTransparency(bool enabled, float opacity)
        {
            opacity = Mathf.Clamp01(opacity);
            if (disposed || (fadeSurroundings == enabled && Mathf.Approximately(surroundingsOpacity, opacity)))
            {
                return;
            }
            var selected = new HashSet<Renderer>(selectionTints.Keys);
            fadeSurroundings = enabled;
            surroundingsOpacity = opacity;
            RefreshTints(selected);
        }

        // UI 위·드래그 중에는 마우스 강조만 해제하고 설명을 연 부품의 선택 색상은 유지합니다.
        public void ClearHover()
        {
            SetHighlighted(null);
        }

        public void Clear()
        {
            ClearHover();
            foreach (var tint in selectionTints.Values)
            {
                tint.Clear();
            }
            selectionTints.Clear();
            transparency.Clear();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            Clear();
            disposed = true;
            foreach (var part in parts)
            {
                if (part.OwnsCollider && part.Collider != null)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(part.Collider);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(part.Collider);
                    }
                }
            }
            parts.Clear();
        }
    }
}
