// <copyright file="ParallelToolSystem.Update.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools.Parallel {
    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Tools;
    using NetworkTools.Components;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    ///     Update logic for the Parallel tool.
    /// </summary>
    public partial class NT_ParallelToolSystem {
        /// <inheritdoc />
        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            UpdateActions();

            // ═══════════════════════════════════════════════════════════════════════════
            // HANDLE INTERACTION PIPELINE 
            // ═══════════════════════════════════════════════════════════════════════════

            if (Phase == OperationPhase.Ready && ProcessHandleInput()) {
                // Handle consumed input (e.g., adjusting offset)
                return HandleTempEntities(inputDeps);
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // NODE SELECTION: Input Detection 
            // ═══════════════════════════════════════════════════════════════════════════

            var rightClickPressed = m_SecondaryApplyAction.WasPressedThisFrame();
            var leftClickPressed = m_ApplyAction.WasPressedThisFrame();
            var raycastHit    = GetRaycastResult(out var controlPoint);
            var hoveredEntity = raycastHit ? controlPoint.m_OriginalEntity : Entity.Null;

            // ═══════════════════════════════════════════════════════════════════════════
            // NODE SELECTION: State Mutation
            // Phase is automatically updated by HandleAddNode/HandleRemoveNode
            // ═══════════════════════════════════════════════════════════════════════════

            if (rightClickPressed) {
                HandleRemoveNode();
                m_UpdateNeeded = true;
            }
            else if (raycastHit) {
                // Update hover state
                if (hoveredEntity != m_LastHoveredEntity.Value) {
                    HandlePathUpdate(controlPoint);
                    HandleHover(hoveredEntity);
                    m_UpdateNeeded = true;
                }
                m_LastHoveredEntity.Value = hoveredEntity;

                // Left-click: add node
                if (leftClickPressed && hoveredEntity != Entity.Null) {
                    HandleAddNode(hoveredEntity);
                    m_UpdateNeeded = true;
                }
            }
            else {
                HandleNoHover();
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // OUTPUT
            // ═══════════════════════════════════════════════════════════════════════════

            return HandleTempEntities(inputDeps);
        }

        /// <summary>
        ///     Handles temp entity generation based on current phase.
        /// </summary>
        private JobHandle HandleTempEntities(JobHandle inputDeps) {
            // For this proof-of-concept, just log phase transitions
            // A full implementation would generate preview entities here

            if (!m_UpdateNeeded) {
                return inputDeps;
            }

            m_UpdateNeeded = false;

            switch (Phase) {
                case OperationPhase.Ready:
                    m_Log.Debug($"ParallelTool: Ready phase");
                    m_Log.Debug($"  Path has {PathNodeCount} nodes and {PathEdgeCount} edges");
                    break;

                case OperationPhase.Applying:
                    m_Log.Debug("ParallelTool: Applying - would create actual parallel road entities");
                    // After apply, transition back to Ready or Idle
                    Phase = OperationPhase.Ready;
                    break;

                case OperationPhase.Idle:
                case OperationPhase.Configuring:
                    // Nothing to preview yet
                    break;
            }

            return inputDeps;
        }

        /// <summary>
        ///     Requests the tool to apply the current transformation.
        /// </summary>
        public void RequestApply() {
            if (Phase != OperationPhase.Ready) {
                return;
            }

            Phase = OperationPhase.Applying;
            m_UpdateNeeded = true;
        }

        /// <inheritdoc />
        protected override bool GetRaycastResult(out ControlPoint controlPoint) {
            if (base.GetRaycastResult(out var entity, out RaycastHit raycastHit)) {
                controlPoint = FilterRaycastResult(entity, raycastHit);
                return controlPoint.m_OriginalEntity != Entity.Null;
            }

            controlPoint = default;
            return false;
        }

        /// <summary>
        ///     Filters raycast results to find the nearest eligible node.
        /// </summary>
        private ControlPoint FilterRaycastResult(Entity entity, RaycastHit hit) {
            var controlPoint = default(ControlPoint);
            var candidateEntity = Entity.Null;

            // If we hit an edge, find the closest node
            if (EntityManager.HasComponent<Edge>(entity)) {
                var edge = EntityManager.GetComponentData<Edge>(entity);
                var startNode = EntityManager.GetComponentData<Node>(edge.m_Start);
                var distanceToStart = math.distance(hit.m_Position, startNode.m_Position);
                var endNode = EntityManager.GetComponentData<Node>(edge.m_End);
                var distanceToEnd = math.distance(hit.m_Position, endNode.m_Position);

                if (distanceToStart < MaxDistanceToSelect && distanceToStart < distanceToEnd) {
                    candidateEntity = edge.m_Start;
                }
                else if (distanceToEnd < MaxDistanceToSelect && distanceToEnd < distanceToStart) {
                    candidateEntity = edge.m_End;
                }
            }
            else {
                candidateEntity = entity;
            }

            // Check eligibility
            if (EntityManager.HasComponent<NT_Eligible>(candidateEntity)) {
                controlPoint = new ControlPoint(candidateEntity, hit);
            }

            return controlPoint;
        }

        /// <inheritdoc />
        public override void InitializeRaycast() {
            base.InitializeRaycast();

            m_ToolRaycastSystem.collisionMask =
                CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.Underground;
            m_ToolRaycastSystem.typeMask = TypeMask.Net;
            m_ToolRaycastSystem.netLayerMask = Layer.All;
            m_ToolRaycastSystem.iconLayerMask = IconLayerMask.None;
            m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;
            m_ToolRaycastSystem.raycastFlags = RaycastFlags.Markers | RaycastFlags.ElevateOffset |
                                               RaycastFlags.SubElements |
                                               RaycastFlags.Cargo | RaycastFlags.Passenger;
        }

        /// <summary>
        ///     Resets the tool to idle state.
        /// </summary>
        public void ResetToIdle() {
            Phase = OperationPhase.Idle;
            DestroyAllHandles();
            ClearSelectionState();
        }
    }
}
