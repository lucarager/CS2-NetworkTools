namespace NetworkTools.Systems.Tools {
    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Tools;
    using NetworkTools.Components;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    ///     Represents the current selection state of path-based tools.
    /// </summary>
    public enum SuperNodeSelectionState {
        NoSelection       = 0,
        StartNodeSelected = 1
    }

    /// <summary>
    ///     # Super Node System
    /// </summary>
    public partial class NT_SuperNodeToolSystem : NT_BaseToolSystem, IToolPrefabProvider, IManualApplyProvider,
                                                  INodeSelectionProvider {
        protected       NativeList<Entity> m_SelectedNodes;
        public override string             toolID => "SuperNode Tool";


        /// <inheritdoc />
        public int ApplyMinNodeCount => 2;

        /// <inheritdoc />
        public bool CanApply => Phase == OperationPhase.Ready;

        /// <summary>
        ///     Requests the tool to apply the current transformation.
        /// </summary>
        public void RequestApply() {
            m_Log.Debug($"RequestApply() -- Selected Nodes: {m_SelectedNodes.Length}");

            if (Phase != OperationPhase.Ready) {
                return;
            }

            Phase = OperationPhase.Applying;
        }

        /// <summary>
        ///     Gets the array of user-selected node entities (path endpoints).
        /// </summary>
        /// <returns>Array of selected Entity objects.</returns>
        public Entity[] GetSelectedNodes() {
            return m_SelectedNodes.ToArray(Allocator.Temp).ToArray();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            UpdateActions();

            var          rightClickPressed = m_SecondaryApplyAction.WasPressedThisFrame();
            var          leftClickPressed  = m_ApplyAction.WasPressedThisFrame();
            var          raycastHit        = false;
            var          hoveredEntity     = Entity.Null;
            var          hitPosition       = float3.zero;
            ControlPoint controlPoint      = default;

            raycastHit = GetRaycastResult(out controlPoint);
            if (raycastHit) {
                hoveredEntity = controlPoint.m_OriginalEntity;
                hitPosition   = controlPoint.m_HitPosition;
            }

            // Right-click: cancel/back (skips all raycast processing)
            if (rightClickPressed) {
                HandleCancel();
                m_UpdateNeeded = true;
            } // Raycast-based interactions
            else if (raycastHit) {
                // Update hover state first
                var newEntityHovered = (hoveredEntity != m_LastHoveredEntity.Value);
                if (newEntityHovered) {
                    HandleHover(controlPoint);
                }

                m_LastHoveredEntity.Value = hoveredEntity;

                // Left-click: add node 
                if (leftClickPressed && hoveredEntity != Entity.Null) {
                    HandleAddNode(hoveredEntity);
                    m_UpdateNeeded = true;
                }
            }
            // No raycast hit
            else {
                HandleNoHover();
            }

            // Handle temp entities
            return HandleTempEntities(inputDeps);
        }

        private void UpdatePhase() {
            switch (m_SelectedNodes.Length) {
                case 0:
                    Phase = OperationPhase.Idle;
                    break;
                case 1:
                    Phase = OperationPhase.Configuring;
                    break;
                default:
                    Phase = OperationPhase.Ready;
                    break;
            }
        }

        private void HandleCancel() {
            if (m_SelectedNodes.Length == 0) {
                m_Log.Debug("Cancel pressed, exiting tool.");
                RequestDisable();
            } else {
                HandleRemoveNode();
            }
        }

        private bool HandleAddNode(Entity entity) {
            if (entity == Entity.Null || m_SelectedNodes.Contains(entity)) {
                return false;
            }

            m_Log.Debug($"Adding node: {entity}");

            // Add node to selection and mark with state-specific components
            m_SelectedNodes.Add(entity);
            EntityManager.AddComponentData(entity,
                                           NT_Selected.ForNode(NodeRenderMode.RenderSelected |
                                                               NodeRenderMode.RenderAsCircle));

            UpdatePhase();

            RefreshEligibility();

            return true;
        }

        private bool HandleRemoveNode() {
            if (m_SelectedNodes.Length == 0) {
                return false;
            }

            var lastNode = m_SelectedNodes.Length > 0 ? m_SelectedNodes[^1] : Entity.Null;

            m_Log.Debug($"Removing node: {lastNode}");

            EntityManager.RemoveComponent<NT_Selected>(lastNode);
            m_SelectedNodes.RemoveAt(m_SelectedNodes.Length - 1);

            UpdatePhase();

            RefreshEligibility();

            return true;
        }

        /// <summary>
        ///     Runs various jobs depending on whether we need to Update, Apply, or Cancel temp entities.
        ///     For the remove node tool, we show preview when hovering over a valid node.
        /// </summary>
        /// <param name="inputDeps"></param>
        /// <param name="updateNeeded"></param>
        /// <returns>inputDeps</returns>
        private JobHandle HandleTempEntities(JobHandle inputDeps) {
            return Phase switch {
                // Preview temp entities
                OperationPhase.Ready => Update(inputDeps),
                // Apply real entities
                OperationPhase.Applying => Apply(inputDeps),
                // Clear otherwise
                OperationPhase.Idle or OperationPhase.Configuring => Clear(inputDeps),
                _                                                 => Clear(inputDeps)
            };
        }

        private void HandleNoHover() {
            m_LastHoveredEntity.Value = Entity.Null;
            ClearAllHighlights();
        }

        private void HandleHover(ControlPoint controlPoint) {
            SwapHighlightedEntities(m_LastHoveredEntity.Value,
                                    controlPoint.m_OriginalEntity,
                                    NT_Highlighted.DefaultNode);
        }

        protected override bool GetRaycastResult(out ControlPoint controlPoint) =>
            TryGetNodeRaycast(out controlPoint);
    }
}
