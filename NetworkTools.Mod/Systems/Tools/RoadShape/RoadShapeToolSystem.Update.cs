namespace NetworkTools.Systems.Tools.RoadShape {
    using Game.Common;
    using Game.Notifications;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;

    using NetworkTools.Components.Handles;
    using NetworkTools.Components;
    using NetworkTools.Components.Tools;

    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Jobs;
    using Unity.Collections;

    public partial class NT_RoadShapeToolSystem {
        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            UpdateActions();

            // ═══════════════════════════════════════════════════════════════════════════
            // HANDLE INTERACTION PIPELINE 
            // ═══════════════════════════════════════════════════════════════════════════

            if (Phase == OperationPhase.Ready && ProcessHandleInput(inputDeps)) {
                // Handle consumed input this frame:
                // - OnHandleDragging() may have updated parameters
                // - m_UpdateNeeded was set to true
                // - Skip node selection, go straight to output
                return HandleTempEntities(inputDeps);
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // NODE SELECTION: Input Detection 
            // ═══════════════════════════════════════════════════════════════════════════

            var rightClickPressed = m_SecondaryApplyAction.WasPressedThisFrame();
            var leftClickPressed = m_ApplyAction.WasPressedThisFrame();
            var raycastHit = false;
            var hoveredEntity = Entity.Null;
            var hitPosition = float3.zero;
            ControlPoint controlPoint = default;

            raycastHit = GetRaycastResult(out controlPoint);
            if (raycastHit) {
                hoveredEntity = controlPoint.m_OriginalEntity;
                hitPosition = controlPoint.m_HitPosition;
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // NODE SELECTION: State Mutation
            // Phase is automatically updated by HandleAddNode/HandleRemoveNode
            // ═══════════════════════════════════════════════════════════════════════════

            // Right-click: cancel/back (skips all raycast processing)
            if (rightClickPressed) {
                HandleRemoveNode();
                m_UpdateNeeded = true;
            }
            // Raycast-based interactions
            else if (raycastHit) {
                // Update hover state first (so path preview is ready if user clicks)
                var newEntityHovered = (hoveredEntity != m_LastHoveredEntity.Value);
                if (newEntityHovered) {
                    HandlePathUpdate(controlPoint);
                    HandleHover(hoveredEntity);
                    m_UpdateNeeded = true;
                }
                m_LastHoveredEntity.Value = hoveredEntity;
                m_LastHitPosition = hitPosition;

                // Left-click: add node (after hover update, same frame OK)
                if (leftClickPressed && hoveredEntity != Entity.Null) {
                    HandleAddNode(hoveredEntity);
                    m_UpdateNeeded = true;
                }
            }
            // No raycast hit
            else {
                HandleNoHover();
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // OUTPUT
            // ═══════════════════════════════════════════════════════════════════════════

            return HandleTempEntities(inputDeps);
        }

        /// <summary>
        ///     Runs various jobs depending on whether we need to Update, Apply, or Cancel temp entities.
        /// </summary>
        /// <param name="inputDeps">Input job dependencies.</param>
        /// <returns>Output job handle.</returns>
        private JobHandle HandleTempEntities(JobHandle inputDeps) {
            return Phase switch {
                // Preview temp entities
                OperationPhase.Ready => Update(inputDeps),
                // Apply real entities
                OperationPhase.Applying => Apply(inputDeps),
                // Clear otherwise
                OperationPhase.Idle or OperationPhase.Configuring => Clear(inputDeps),
                _ => Clear(inputDeps)
            };
        }

        /// <inheritdoc />
        public int ApplyMinNodeCount => 2;

        /// <inheritdoc />
        public bool CanApply => Phase == OperationPhase.Ready && Template.Value != ShapeTransformTemplate.Preserve;

        /// <summary>
        ///     Requests the tool to apply the current transformation.
        /// </summary>
        public void RequestApply() {
            if (Phase != OperationPhase.Ready) {
                return;
            }

            Phase = OperationPhase.Applying;
        }

        protected override bool GetRaycastResult(out ControlPoint controlPoint) =>
            TryGetNodeRaycast(out controlPoint);

        /// <summary>
        ///     Resets the tool to idle state, clearing all selection.
        /// </summary>
        public void ResetToIdle() {
            // Clear state to completely blank
            Phase = OperationPhase.Idle;

            // Destroy any active handles
            DestroyAllHandles();

            // Use base class method to clear selection state
            ClearSelectionState();
        }
    }
}