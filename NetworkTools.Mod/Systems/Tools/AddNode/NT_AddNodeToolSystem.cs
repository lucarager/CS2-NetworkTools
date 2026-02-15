// <copyright file="NT_CEToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Colossal.Entities;
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
    public partial class NT_AddNodeToolSystem : NT_BaseToolSystem {
        private ControlPoint m_LastControlPoint;

        public override string toolID => "AddNode Tool";

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            UpdateActions();

            var updateNeeded = false;

            // Get raycast result
            if (GetRaycastResult(out var controlPoint)) {
                // We hit something
                var newEntityWasHit = m_LastHoveredEntity.Value != controlPoint.m_OriginalEntity;
                updateNeeded = newEntityWasHit;

                HandleHover(controlPoint);

                // Snap
                //SnapControlPoints(inputDeps);

                // Handle clicking
                if (m_ApplyAction.WasPressedThisFrame()) {
                    HandleApply(controlPoint.m_OriginalEntity);
                }
            } else {
                // No entity under cursor
                HandleNoHover();
            }

            // Debug
            var buffer = m_OverlayRenderSystem.GetBuffer(out var deps);
            inputDeps = JobHandle.CombineDependencies(inputDeps, deps);
            if (controlPoint.m_OriginalEntity != Entity.Null) {
                buffer.DrawCircle(UnityEngine.Color.white, controlPoint.m_Position, 3f);
            }

            // Handle temp entities
            return HandleTempEntities(inputDeps, updateNeeded);
        }

        private void HandleApply(Entity controlPointEntity) {
            Phase = OperationPhase.Ready;
        }

        /// <summary>
        /// Runs various jobs depending on whether we need to Update, Apply, or Cancel temp entities
        /// </summary>
        /// <param name="inputDeps"></param>
        /// <returns>inputDeps</returns>
        private JobHandle HandleTempEntities(JobHandle inputDeps, bool updateNeeded) {
            return Phase switch
            {
                // No temp entities needed
                OperationPhase.Idle => inputDeps,
                // Preview temp entities
                OperationPhase.Ready or OperationPhase.Configuring => Update(inputDeps, updateNeeded),
                // Apply real entities
                OperationPhase.Applying => Apply(inputDeps),
                // Clear otherwise
                _ => Clear(inputDeps),
            };
        }

        private void HandleNoHover() {
            RemoveHighlight(m_LastHoveredEntity.Value);
            m_LastHoveredEntity.Value = Entity.Null;
            m_LastControlPoint        = default;
            Phase    = OperationPhase.Idle;
        }

        private void HandleHover(ControlPoint controlPoint) {
            AddHighlight(controlPoint.m_OriginalEntity);
            // Update Cache
            m_LastHoveredEntity.Value = controlPoint.m_OriginalEntity;
            m_LastControlPoint        = controlPoint;
            Phase    = OperationPhase.Configuring;
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
            var controlPoint    = default(ControlPoint);
            var candidateEntity = Entity.Null;
            var curvePosition   = 0f;

            // If we hit a node, find the closest edge instead
            if (EntityManager.HasComponent<Node>(entity)) {
                // todo make job
                // Find the closest edge to the hit position from connected edges
                if (EntityManager.TryGetBuffer<ConnectedEdge>(entity, true, out var connectedEdges)) {
                    var bestDistance = MaxDistanceToSelect;

                    for (var i = 0; i < connectedEdges.Length; i++) {
                        var edgeEntity = connectedEdges[i].m_Edge;
                        if (!EntityManager.TryGetComponent<Curve>(edgeEntity, out var curve)) {
                            continue;
                        }

                        var distance = Colossal.Mathematics.MathUtils.Distance(curve.m_Bezier.xz, hit.m_Position.xz, out var t);
                        if (distance < bestDistance) {
                            bestDistance    = distance;
                            candidateEntity = edgeEntity;
                            curvePosition   = t;
                        }
                    }
                }
            } else if (EntityManager.HasComponent<Edge>(entity)) {
                // Edge hit directly, compute curve position
                candidateEntity = entity;
                if (EntityManager.TryGetComponent<Curve>(entity, out var curve)) {
                    Colossal.Mathematics.MathUtils.Distance(curve.m_Bezier.xz, hit.m_Position.xz, out curvePosition);
                }
            }

            // Check that the entity we're hitting is eligible
            if (candidateEntity != Entity.Null) {
                controlPoint = new ControlPoint(candidateEntity, hit);
                controlPoint.m_CurvePosition = curvePosition;
            }

            return controlPoint;
        }

        public override void InitializeRaycast() {
            base.InitializeRaycast();

            m_ToolRaycastSystem.collisionMask   = CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.Underground;
            m_ToolRaycastSystem.typeMask        = TypeMask.Net;
            m_ToolRaycastSystem.netLayerMask    = Layer.All;
            m_ToolRaycastSystem.iconLayerMask   = IconLayerMask.None;
            m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;
            m_ToolRaycastSystem.raycastFlags = RaycastFlags.Markers | RaycastFlags.ElevateOffset | RaycastFlags.SubElements |
                                               RaycastFlags.Cargo   | RaycastFlags.Passenger;
        }
    }
}