using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SmartTraining
{
    // 모델의 표시·클릭 판정·로컬 드래그만 담당합니다. 공유 회전은 부모 SharedModel의 NetworkTransform이 동기화합니다.
    public sealed class ModelViewer : MonoBehaviour
    {
        [SerializeField]
        private Transform modelRoot;

        private readonly List<Renderer> renderers = new();
        private SharedModel sharedModel;
        private Vector3 centerInModel;
        private bool controlRequested;
        private ModelPartHighlighter partHighlighter;
        private readonly ModelPointerGesture pointerGesture = new();
        private Renderer pressedPart;
        private ModelDisassemblyAnimation disassembly;
        private IReadOnlyList<ModelDisassemblyAnimation.Step> steps = System.Array.Empty<ModelDisassemblyAnimation.Step>();
        private ModelExplodedView explodedView;
        private float displayedExplosion;
        private int lastPlaybackDirection;
        private ModelPartTree partTree;
        private readonly List<ModelPartTree.Node> selectedNodes = new();
        private readonly List<Renderer> selectedRenderers = new();

        internal IReadOnlyList<ModelPartTree.Node> Parts => partTree.Nodes;
        internal IReadOnlyList<ModelPartTree.Node> SelectedNodes => selectedNodes;
        internal IReadOnlyList<Renderer> SelectedRenderers => selectedRenderers;
        internal Transform SelectedPart => selectedNodes.Count > 0 ? selectedNodes[0].Transform : null;
        internal int SelectionVersion { get; private set; }
        internal string SelectedPartTitle => selectedNodes.Count == 1 ? selectedNodes[0].Title :
            selectedNodes.Count > 1 ? selectedNodes.Count + "개 부품 묶음 선택" : "";
        internal string SelectedPartDescription => string.Join("\n\n", selectedNodes.Select(node =>
            selectedNodes.Count == 1 ? node.Description : node.Title + "\n" + node.Description));

        internal bool IsSelectedNode(ModelPartTree.Node node) => selectedNodes.Contains(node);
        internal bool IsIncludedNode(ModelPartTree.Node node) => node?.Transform != null &&
            selectedNodes.Any(selected => selected.Transform != null && node.Transform.IsChildOf(selected.Transform));

        public bool TryInitialize(out string error)
        {
            Bounds bounds = default;
            foreach (var renderer in (modelRoot != null ? modelRoot : transform).GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                {
                    continue;
                }
                if (renderers.Count == 0)
                {
                    bounds = renderer.bounds;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                renderers.Add(renderer);
            }
            centerInModel = transform.InverseTransformPoint(bounds.center);
            partHighlighter = new ModelPartHighlighter(renderers);
            partTree = new ModelPartTree(modelRoot != null ? modelRoot : transform, renderers);
            disassembly = (modelRoot != null ? modelRoot : transform).GetComponent<ModelDisassemblyAnimation>();
            steps = disassembly != null ? disassembly.GetOrderedSteps() : System.Array.Empty<ModelDisassemblyAnimation.Step>();
            explodedView = new ModelExplodedView(disassembly);
            // 서버에서 스폰한 공용 루트가 도착하기 전에는 중복된 로컬 전시물이 보이지 않게 합니다.
            SetVisible(false);
            error = renderers.Count > 0 ? null : "모델 프리팹에서 표시할 메시를 찾지 못했습니다.";
            return renderers.Count > 0;
        }

        private void Update()
        {
            if (sharedModel == null || !sharedModel.IsSpawned)
            {
                return;
            }
            int previousStep = CurrentStepIndex;
            int direction = sharedModel.PlaybackDirection;
            bool playbackStarted = direction != 0 && lastPlaybackDirection != direction;
            lastPlaybackDirection = direction;
            // 자동 재생은 서버 시각으로 계산해 부드럽게 진행합니다. 수동 구간 이동만 짧게 보간합니다.
            float amount = direction != 0 ? sharedModel.ExplosionAmount :
                Mathf.MoveTowards(displayedExplosion, sharedModel.ExplosionAmount, Time.unscaledDeltaTime * 2.5f);
            if (amount != displayedExplosion)
            {
                displayedExplosion = amount;
                explodedView?.Apply(amount);
            }
            RefreshStepSelection(previousStep, playbackStarted);
        }

        // 처음에는 선택하지 않습니다. 재생 시작·방향 전환·단계 경계를 통과한 경우에만 현재 대상을 선택합니다.
        private void RefreshStepSelection(int previousStep, bool playbackStarted)
        {
            if (playbackStarted || previousStep != CurrentStepIndex)
            {
                SelectStepAtAmount(displayedExplosion);
            }
        }

        private void SelectStepAtAmount(float amount)
        {
            int index = disassembly != null ? disassembly.FindStepIndex(steps, amount) : -1;
            if (index >= 0)
            {
                SelectStepTargets(steps[index]);
            }
            else
            {
                ClearSelection();
            }
        }

        internal int StepCount => steps.Count;
        internal int CurrentStepIndex => disassembly != null ? disassembly.FindStepIndex(steps, displayedExplosion) : -1;
        internal ModelDisassemblyAnimation.Step CurrentStep => CurrentStepIndex >= 0 ? steps[CurrentStepIndex] : null;

        // 버튼·재생·슬라이더가 같은 단계 대상 선택 경로를 사용합니다.
        private void SelectStepTargets(ModelDisassemblyAnimation.Step step)
        {
            var nodes = new List<ModelPartTree.Node>();
            foreach (var target in step.Targets)
            {
                var node = target != null ? partTree.Find(target) : null;
                if (node != null)
                {
                    nodes.Add(node);
                }
            }
            SelectNodes(nodes);
        }

        internal bool CanMoveStep(Camera camera, int direction)
        {
            if (disassembly == null || (direction != -1 && direction != 1) || !CanSetExplosion(camera))
            {
                return false;
            }
            int next = disassembly.FindStepIndex(steps, ExplosionAmount) + direction;
            return next >= 0 && next < steps.Count;
        }

        internal void MoveStep(Camera camera, int direction)
        {
            if (CanMoveStep(camera, direction))
            {
                int next = disassembly.FindStepIndex(steps, ExplosionAmount) + direction;
                // 기존 공유 구간 이동 API를 재사용합니다. 서버가 재생을 멈추고 모든 참가자의 시간을 변경합니다.
                SetExplosion(camera, steps[next].Time / disassembly.Clip.length);
            }
        }

        internal float ExplosionAmount => sharedModel != null && sharedModel.IsSpawned ? sharedModel.ExplosionAmount : 0;

        internal bool CanSetExplosion(Camera camera)
        {
            return explodedView != null && explodedView.IsAvailable && camera != null && sharedModel != null && sharedModel.IsSpawned &&
                (sharedModel.NetworkObject.IsOwnedByServer || sharedModel.IsOwner) &&
                Vector3.Distance(camera.transform.position, sharedModel.transform.position) <= SharedModel.InteractionDistance;
        }

        internal void SetExplosion(Camera camera, float amount)
        {
            if (CanSetExplosion(camera))
            {
                SelectStepAtAmount(amount);
                sharedModel.SetExplosionRpc(amount);
            }
        }

        internal int PlaybackDirection => sharedModel != null && sharedModel.IsSpawned ? sharedModel.PlaybackDirection : 0;

        internal bool CanSetPlayback(Camera camera, int direction)
        {
            return CanSetExplosion(camera) && PlaybackDirection != direction &&
                (direction == 0 ? PlaybackDirection != 0 : direction > 0 ? ExplosionAmount < 1 : ExplosionAmount > 0);
        }

        internal void SetPlayback(Camera camera, int direction)
        {
            if (CanSetPlayback(camera, direction))
            {
                if (direction != 0)
                {
                    SelectStepAtAmount(ExplosionAmount);
                }
                var source = (modelRoot != null ? modelRoot : transform).GetComponent<ModelDisassemblyAnimation>();
                sharedModel.SetPlaybackRpc(direction, source.Clip.length);
            }
        }

        internal string PlaybackStatus => PlaybackDirection > 0 ? "분해 재생 중" : PlaybackDirection < 0 ? "재조립 중" :
            ExplosionAmount <= 0 ? "조립 상태" : ExplosionAmount >= 1 ? "분해 완료" : "일시정지";

        private void SetVisible(bool visible)
        {
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }

        private bool BindSharedModel(RoomPlayer player)
        {
            if (sharedModel != null && sharedModel.IsSpawned)
            {
                return true;
            }
            sharedModel = SharedModel.Find(player.NetworkManager);
            if (sharedModel == null)
            {
                return false;
            }
            // 네트워크 루트의 피벗에 모델 중심을 맞춥니다. 프리팹의 크기·기본 회전은 그대로 유지합니다.
            transform.SetParent(sharedModel.transform, false);
            transform.localPosition = -(transform.localRotation * Vector3.Scale(transform.localScale, centerInModel));
            SetVisible(true);
            return true;
        }

        internal void UpdateInteraction(Camera camera, RoomPlayer player, bool canInteract)
        {
            if (!BindSharedModel(player))
            {
                CancelPointer();
                partHighlighter?.ClearHover();
                return;
            }
            var mouse = Mouse.current;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!canInteract || mouse == null || overUi || mouse.rightButton.isPressed)
            {
                CancelPointer();
                partHighlighter?.ClearHover();
                return;
            }
            bool inRange = Vector3.Distance(camera.transform.position, sharedModel.transform.position) <= SharedModel.InteractionDistance;
            var ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            if (mouse.leftButton.wasPressedThisFrame)
            {
                pointerGesture.Begin(mouse.position.ReadValue(), Time.unscaledTime);
                pressedPart = inRange ? partHighlighter.FindPart(ray, SharedModel.InteractionDistance) : null;
            }
            pointerGesture.Update(mouse.position.ReadValue());
            if (!inRange && pressedPart != null)
            {
                CancelPointer();
            }
            if (pointerGesture.IsDragging && pressedPart != null && !controlRequested && mouse.leftButton.isPressed)
            {
                // 클릭만 했을 때는 소유권을 가져가지 않습니다. 이동 임계값을 넘겼을 때 한 번 요청합니다.
                controlRequested = true;
                sharedModel.RequestControlRpc();
            }
            if (mouse.leftButton.wasReleasedThisFrame && pointerGesture.IsPressed)
            {
                if (pointerGesture.End(mouse.position.ReadValue(), Time.unscaledTime))
                {
                    var releasedPart = inRange ? partHighlighter.FindPart(ray, SharedModel.InteractionDistance) : null;
                    if (releasedPart == pressedPart)
                    {
                        SelectPart(releasedPart);
                    }
                }
                CancelPointer();
            }
            else if (!mouse.leftButton.isPressed)
            {
                CancelPointer();
            }
            partHighlighter.UpdateHover(ray, inRange && !mouse.leftButton.isPressed, SharedModel.InteractionDistance);
            if (controlRequested && pointerGesture.IsDragging && sharedModel.IsOwner)
            {
                var delta = mouse.delta.ReadValue();
                sharedModel.transform.rotation = Quaternion.AngleAxis(-delta.x * 0.2f, camera.transform.up) *
                    Quaternion.AngleAxis(delta.y * 0.2f, camera.transform.right) * sharedModel.transform.rotation;
            }
        }

        private void CancelPointer()
        {
            pointerGesture.Cancel();
            pressedPart = null;
            ReleaseControl();
        }

        private void SelectPart(Renderer part)
        {
            SelectNode(part != null ? partTree.Find(part.transform) : null);
        }

        private void SelectNode(ModelPartTree.Node node)
        {
            SelectNodes(node != null ? new[] { node } : System.Array.Empty<ModelPartTree.Node>());
        }

        // 직접 클릭·트리 선택·단계 이동이 공유하는 단일 선택 집합입니다.
        private void SelectNodes(IReadOnlyList<ModelPartTree.Node> nodes)
        {
            var candidates = new HashSet<ModelPartTree.Node>(nodes);
            selectedNodes.Clear();
            selectedRenderers.Clear();
            var uniqueRenderers = new HashSet<Renderer>();
            if (partTree != null)
            {
                // 부모를 먼저 확인해 부모·자식을 동시에 지정해도 같은 묶음을 중복 선택하지 않습니다.
                foreach (var node in partTree.Nodes)
                {
                    if (!candidates.Contains(node) || node.Transform == null || !node.Transform.gameObject.activeInHierarchy ||
                        selectedNodes.Any(parent => node.Transform.IsChildOf(parent.Transform)))
                    {
                        continue;
                    }
                    selectedNodes.Add(node);
                    foreach (var renderer in node.Renderers)
                    {
                        if (renderer != null && uniqueRenderers.Add(renderer))
                        {
                            selectedRenderers.Add(renderer);
                        }
                    }
                }
            }
            partHighlighter?.SetSelection(selectedRenderers);
            // 같은 부품을 다시 선택해도 접힌 선택 경로를 펼칩니다. 네트워크 상태가 아닌 개인별 UI 버전입니다.
            SelectionVersion++;
        }

        internal bool SelectPartFromList(ModelPartTree.Node node)
        {
            if (node?.Transform == null || partTree.Find(node.Transform) != node || !node.Transform.gameObject.activeInHierarchy)
            {
                return false;
            }
            SelectNode(node);
            return true;
        }

        internal void ClearSelection()
        {
            SelectNode(null);
        }

        // 본인 화면의 표시만 변경합니다. 회전 소유권·RPC·다른 참가자의 재질에는 영향을 주지 않습니다.
        internal void SetSurroundingsTransparency(bool enabled, float opacity)
        {
            partHighlighter?.SetSurroundingsTransparency(enabled, opacity);
        }

        private void ReleaseControl()
        {
            if (controlRequested && sharedModel != null && sharedModel.IsSpawned)
            {
                sharedModel.ReleaseControlRpc(sharedModel.transform.rotation);
            }
            controlRequested = false;
        }

        internal bool IsBeingManipulated => sharedModel != null && sharedModel.IsSpawned && !sharedModel.NetworkObject.IsOwnedByServer;

        internal bool IsLocalManipulator => IsBeingManipulated && sharedModel.IsOwner;

        // 이미 동기화된 소유권과 참가자 닉네임을 사용합니다. 표시를 위한 별도 RPC나 복제 변수는 만들지 않습니다.
        internal string GetControlStatus()
        {
            if (sharedModel == null || !sharedModel.IsSpawned)
            {
                return "모델을 준비하고 있습니다.";
            }
            if (!IsBeingManipulated)
            {
                return "모델 조작 가능 · 모델을 드래그해 회전하세요.";
            }
            // 클라이언트에서도 보이는 SpawnedObjects에서 찾습니다. 서버 전용 참가자 조회 API는 사용하지 않습니다.
            foreach (var spawned in sharedModel.NetworkManager.SpawnManager.SpawnedObjects.Values)
            {
                if (spawned.OwnerClientId == sharedModel.OwnerClientId && spawned.TryGetComponent<RoomPlayer>(out var participant) &&
                    !string.IsNullOrWhiteSpace(participant.UserName))
                {
                    return participant.UserName + " 님이 모델을 조작 중입니다.";
                }
            }
            // 소유권 메시지가 닉네임보다 먼저 도착하거나 참가자가 정리되는 짧은 구간의 안내입니다.
            return "참가자가 모델을 조작 중입니다.";
        }

        // 기능 메뉴의 버튼 상태와 클릭 처리가 같은 조건을 사용합니다. 실제 공유 상태 변경은 서버에서 검사합니다.
        internal bool CanResetRotation(Camera camera)
        {
            return camera != null && sharedModel != null && sharedModel.IsSpawned &&
                sharedModel.NetworkObject.IsOwnedByServer &&
                Vector3.Distance(camera.transform.position, sharedModel.transform.position) <= SharedModel.InteractionDistance;
        }

        internal void ResetRotation(Camera camera)
        {
            if (CanResetRotation(camera))
            {
                sharedModel.ResetRotationRpc();
            }
        }

        private void OnDisable()
        {
            partHighlighter?.Clear();
            explodedView?.Apply(0);
            displayedExplosion = 0;
            lastPlaybackDirection = 0;
            CancelPointer();
            ClearSelection();
        }

        private void OnDestroy()
        {
            partHighlighter?.Dispose();
            explodedView?.Dispose();
        }
    }
}
