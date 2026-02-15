// <copyright file="NT_CEToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Game.Common;
    using Game.Input;
    using Game.Net;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using NetworkTools.Systems.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// # Remove Node System
    /// </summary>
    public partial class NT_RemoveNodeToolSystem : NT_BaseToolSystem {
        public override string toolID => "RemoveNode Tool";

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            UpdateActions();

            var updateNeeded = false;

            // Get raycast result
            if (GetRaycastResult(out var controlPoint)) {
                // We hit something
                var newEntityWasHit = m_LastHoveredEntity.Value != controlPoint.m_OriginalEntity;
                updateNeeded = newEntityWasHit;

                if (newEntityWasHit) {
                    HandleHover(controlPoint);
                }

                // Update Cache
                m_LastHoveredEntity.Value = controlPoint.m_OriginalEntity;

                // Handle clicking
                if (m_ApplyAction.WasPressedThisFrame()) {
                    HandleApply();
                }
            }
            else {
                // No entity under cursor
                HandleNoHover();
            }

            // Handle temp entities
            return HandleTempEntities(inputDeps, updateNeeded);
        }

        private void HandleApply() {
            Phase = OperationPhase.Applying;
        }

        /// <summary>
        /// Runs various jobs depending on whether we need to Update, Apply, or Cancel temp entities.
        /// For the remove node tool, we show preview when hovering over a valid node.
        /// </summary>
        /// <param name="inputDeps"></param>
        /// <param name="updateNeeded"></param>
        /// <returns>inputDeps</returns>
        private JobHandle HandleTempEntities(JobHandle inputDeps, bool updateNeeded) {
            return Phase switch {
                OperationPhase.Ready =>
                    // Show preview when hovering over a valid (eligible) node
                    Update(inputDeps, updateNeeded),
                OperationPhase.Applying =>
                    // Apply removal
                    Apply(inputDeps),
                OperationPhase.Idle =>
                    // Clear preview when not hovering
                    Clear(inputDeps),
                _ => Clear(inputDeps)
            };
        }

        private void HandleNoHover() {
            m_LastHoveredEntity.Value = Entity.Null;
            Phase    = OperationPhase.Idle;
            ClearAllHighlights();
        }

        private void HandleHover(ControlPoint controlPoint) {
            Phase = OperationPhase.Ready;
            SwapHighlightedEntities(m_LastHoveredEntity.Value, controlPoint.m_OriginalEntity);
        }

        protected override bool GetRaycastResult(out ControlPoint controlPoint) {
            if (base.GetRaycastResult(out var entity, out RaycastHit raycastHit)) {
                controlPoint = FilterRaycastResult(entity, raycastHit);
                return controlPoint.m_OriginalEntity != Entity.Null;
            }

            controlPoint = default;
            return false;
        }

        private ControlPoint FilterRaycastResult(Entity entity, RaycastHit hit) {
            var controlPoint = default(ControlPoint);
            var candidateEntity = Entity.Null;

            // If we hit an edge, find the closest node instead
            if (EntityManager.HasComponent<Edge>(entity)) {
                // todo make job
                // Find the closest node to the hit position
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

            // Check that the entity we're hitting is eligible
            if (EntityManager.HasComponent<Components.NT_Eligible>(candidateEntity)) {
                controlPoint = new ControlPoint(candidateEntity, hit);
            }

            return controlPoint;
        }

        public override void InitializeRaycast() {
            base.InitializeRaycast();

            m_ToolRaycastSystem.collisionMask =
                CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.Underground;
            m_ToolRaycastSystem.typeMask        = TypeMask.Net;
            m_ToolRaycastSystem.netLayerMask    = Layer.All;
            m_ToolRaycastSystem.iconLayerMask   = IconLayerMask.None;
            m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;
            m_ToolRaycastSystem.raycastFlags = RaycastFlags.Markers | RaycastFlags.ElevateOffset |
                                               RaycastFlags.SubElements |
                                               RaycastFlags.Cargo | RaycastFlags.Passenger;
        }
    }
}