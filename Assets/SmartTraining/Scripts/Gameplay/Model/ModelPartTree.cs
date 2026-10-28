using System.Collections.Generic;
using UnityEngine;

namespace SmartTraining
{
    // 실제 Transform 계층에서 모델 부품 트리를 만듭니다. 표시할 메시가 없는 보조 가지는 제외합니다.
    public sealed class ModelPartTree
    {
        public sealed class Node
        {
            private readonly List<Node> children = new();
            private readonly List<Renderer> renderers = new();
            public Transform Transform { get; }
            public Node Parent { get; }
            public int Depth { get; }
            public IReadOnlyList<Node> Children => children;
            public IReadOnlyList<Renderer> Renderers => renderers;
            public string Title
            {
                get
                {
                    if (Transform == null)
                    {
                        return "삭제된 부품";
                    }
                    var info = Transform.GetComponent<ModelPartInfo>();
                    if (info != null)
                    {
                        return info.Title;
                    }
                    string name = Transform.name;
                    return Parent == null && name.EndsWith("(Clone)", System.StringComparison.Ordinal) ? name.Substring(0, name.Length - 7).TrimEnd() : name;
                }
            }
            public string Description
            {
                get
                {
                    for (var node = this; node != null; node = node.Parent)
                    {
                        var info = node.Transform != null ? node.Transform.GetComponent<ModelPartInfo>() : null;
                        if (info != null)
                        {
                            return info.Description;
                        }
                    }
                    return "등록된 부품 설명이 없습니다.";
                }
            }

            internal Node(Transform transform, Node parent)
            {
                Transform = transform;
                Parent = parent;
                Depth = parent != null ? parent.Depth + 1 : 0;
            }

            internal void AddRenderer(Renderer renderer)
            {
                renderers.Add(renderer);
            }

            internal void AddChild(Node child)
            {
                children.Add(child);
                renderers.AddRange(child.renderers);
            }
        }

        private readonly Dictionary<Transform, Node> byTransform = new();
        private readonly List<Node> nodes = new();
        public Node Root { get; }
        public IReadOnlyList<Node> Nodes => nodes;

        public ModelPartTree(Transform root, IReadOnlyList<Renderer> renderers)
        {
            var byObject = new Dictionary<Transform, List<Renderer>>();
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }
                if (!byObject.TryGetValue(renderer.transform, out var owned))
                {
                    owned = new List<Renderer>();
                    byObject.Add(renderer.transform, owned);
                }
                owned.Add(renderer);
            }
            Root = Build(root, null, byObject);
            if (Root != null)
            {
                Collect(Root);
            }
        }

        public Node Find(Transform transform)
        {
            return transform != null && byTransform.TryGetValue(transform, out var node) ? node : null;
        }

        private Node Build(Transform transform, Node parent, Dictionary<Transform, List<Renderer>> renderers)
        {
            var node = new Node(transform, parent);
            if (renderers.TryGetValue(transform, out var owned))
            {
                foreach (var renderer in owned)
                {
                    node.AddRenderer(renderer);
                }
            }
            foreach (Transform child in transform)
            {
                var childNode = Build(child, node, renderers);
                if (childNode != null)
                {
                    node.AddChild(childNode);
                }
            }
            // 빈 부모라도 하위에 메시가 있으면 선택 가능한 묶음으로 유지합니다.
            return node.Renderers.Count > 0 ? node : null;
        }

        private void Collect(Node node)
        {
            nodes.Add(node);
            byTransform.Add(node.Transform, node);
            foreach (var child in node.Children)
            {
                Collect(child);
            }
        }
    }
}
