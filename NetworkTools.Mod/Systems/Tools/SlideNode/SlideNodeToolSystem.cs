// <copyright file="SlideNodeToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using Colossal.Entities;
    using Colossal.Mathematics;
    using Game.Common;
    using Game.Input;
    using Game.Net;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using NetworkTools.Components;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    /// Slide Node Tool — allows dragging an intermediate node along the parent bezier
    /// curve formed by its two connected edges, preserving exact curve shape via
    /// de Casteljau subdivision.
    /// </summary>
    public partial class NT_SlideNodeToolSystem : NT_BaseToolSystem, IToolPrefabProvider {
        private ControlPoint m_LastControlPoint;
        private bool         m_UpdateNeeded;

        /// <summary>Whether the user is currently dragging a node.</summary>
        private bool m_IsDragging;

        /// <summary>The node entity being dragged.</summary>
        private Entity m_DragNodeEntity;

        /// <summary>The two edge entities connected to the drag node.</summary>
        private Entity m_Edge1Entity;
        private Entity m_Edge2Entity;

        /// <summary>The recovered parent bezier from the two child edges.</summary>
        private Bezier4x3 m_ParentBezier;

        /// <summary>The snapped curve position on the parent bezier (0..1).</summary>
        private float m_SnappedCurvePosition;

        public override string toolID => "SlideNode Tool";

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            UpdateActions();

            // Right click => Cancel drag or exit tool
            if (m_SecondaryApplyAction.WasPressedThisFrame()) {
                if (m_IsDragging) {
                    // Cancel the drag — revert to idle, clear preview
                    CancelDrag();
                    return HandleTempEntities(inputDeps);
                }

                CancelHandleInteraction();
                HandleCancel();
                return inputDeps;
            }

            // Get raycast result
            if (GetRaycastResult(out var controlPoint)) {
                if (m_IsDragging) {
                    // While dragging: project hit position onto parent bezier
                    SnapControlPoint(controlPoint.m_HitPosition, inputDeps);
                    m_LastControlPoint = controlPoint;
                    m_UpdateNeeded = true;
                    Phase = OperationPhase.Configuring;

                    // Release => apply
                    if (m_ApplyAction.WasReleasedThisFrame()) {
                        Phase = OperationPhase.Applying;
                    }
                } else {
                    // Not dragging: hover mode — highlight eligible nodes
                    var newEntityWasHit = m_LastHoveredEntity.Value != controlPoint.m_OriginalEntity;
                    m_UpdateNeeded = newEntityWasHit;

                    if (newEntityWasHit) {
                        HandleHover(controlPoint);
                    }

                    m_LastHoveredEntity.Value = controlPoint.m_OriginalEntity;
                    m_LastControlPoint = controlPoint;

                    // Press => start drag
                    if (m_ApplyAction.WasPressedThisFrame() && controlPoint.m_OriginalEntity != Entity.Null) {
                        StartDrag(controlPoint.m_OriginalEntity);
                    }
                }
            } else {
                if (m_IsDragging) {
                    // Dragging but cursor moved off valid geometry — keep last position
                    m_UpdateNeeded = false;

                    // Release => apply with last known position
                    if (m_ApplyAction.WasReleasedThisFrame()) {
                        Phase = OperationPhase.Applying;
                    }
                } else {
                    HandleNoHover();
                }
            }

            return HandleTempEntities(inputDeps);
        }

        /// <summary>
        /// Starts a drag operation on the given node entity.
        /// Captures the two connected edges and computes the initial parent bezier.
        /// </summary>
        private void StartDrag(Entity nodeEntity) {
            if (!EntityManager.HasBuffer<ConnectedEdge>(nodeEntity)) {
                return;
            }

            var connectedEdges = EntityManager.GetBuffer<ConnectedEdge>(nodeEntity);
            if (connectedEdges.Length != 2) {
                return;
            }

            m_DragNodeEntity = nodeEntity;
            m_Edge1Entity = connectedEdges[0].m_Edge;
            m_Edge2Entity = connectedEdges[1].m_Edge;

            // Compute initial parent bezier
            if (EntityManager.TryGetComponent<Edge>(m_Edge1Entity, out var edge1) &&
                EntityManager.TryGetComponent<Edge>(m_Edge2Entity, out var edge2) &&
                EntityManager.TryGetComponent<Curve>(m_Edge1Entity, out var curve1) &&
                EntityManager.TryGetComponent<Curve>(m_Edge2Entity, out var curve2)) {
                // Orient and merge curves
                var b1 = edge1.m_Start == nodeEntity ? MathUtils.Invert(curve1.m_Bezier) : curve1.m_Bezier;
                var b2 = edge2.m_End == nodeEntity ? MathUtils.Invert(curve2.m_Bezier) : curve2.m_Bezier;
                m_ParentBezier = new Bezier4x3 { a = b1.a, b = b1.b, c = b2.c, d = b2.d };

                // Find current node position on parent curve as initial parameter
                var nodePos = EntityManager.GetComponentData<Node>(nodeEntity).m_Position;
                MathUtils.Distance(m_ParentBezier, nodePos, out m_SnappedCurvePosition);
            }

            m_IsDragging = true;
            m_UpdateNeeded = true;
            Phase = OperationPhase.Configuring;

            m_Log.Debug($"StartDrag: node={nodeEntity}, edge1={m_Edge1Entity}, edge2={m_Edge2Entity}");
        }

        /// <summary>
        /// Cancels the current drag operation and reverts to idle.
        /// </summary>
        private void CancelDrag() {
            m_Log.Debug("CancelDrag: reverting to idle");
            m_IsDragging = false;
            m_DragNodeEntity = Entity.Null;
            m_Edge1Entity = Entity.Null;
            m_Edge2Entity = Entity.Null;
            Phase = OperationPhase.Idle;
        }

        private void HandleCancel() {
            m_Log.Debug("Cancel pressed, exiting tool.");
            RequestDisable();
        }

        /// <summary>
        /// Routes to the appropriate job method based on the current operation phase.
        /// </summary>
        private JobHandle HandleTempEntities(JobHandle inputDeps) {
            return Phase switch {
                OperationPhase.Idle => Clear(inputDeps),
                OperationPhase.Configuring => Update(inputDeps),
                OperationPhase.Ready => Update(inputDeps),
                OperationPhase.Applying => Apply(inputDeps),
                _ => Clear(inputDeps),
            };
        }

        private void HandleNoHover() {
            m_LastHoveredEntity.Value = Entity.Null;
            m_LastControlPoint = default;
            Phase = OperationPhase.Idle;
            ClearAllHighlights();
        }

        private void HandleHover(ControlPoint controlPoint) {
            Phase = OperationPhase.Ready;
            SwapHighlightedEntities(m_LastHoveredEntity.Value, controlPoint.m_OriginalEntity, NT_Highlighted.DefaultNode);
        }

        protected override bool GetRaycastResult(out ControlPoint controlPoint) {
            if (base.GetRaycastResult(out var entity, out RaycastHit raycastHit)) {
                controlPoint = FilterRaycastResult(entity, raycastHit);
                return controlPoint.m_OriginalEntity != Entity.Null || m_IsDragging;
            }

            controlPoint = default;
            return m_IsDragging;
        }

        /// <summary>
        /// Filters the raycast result to target eligible nodes.
        /// If an edge is hit, finds the closest eligible node.
        /// During drag, always returns a valid control point with the hit position.
        /// </summary>
        private ControlPoint FilterRaycastResult(Entity entity, RaycastHit hit) {
            var controlPoint = default(ControlPoint);

            if (m_IsDragging) {
                // During drag, we only need the hit position — the node is already captured
                controlPoint = new ControlPoint(m_DragNodeEntity, hit);
                return controlPoint;
            }

            var candidateEntity = Entity.Null;

            // If we hit an edge, find the closest eligible node
            if (EntityManager.HasComponent<Edge>(entity)) {
                var edge = EntityManager.GetComponentData<Edge>(entity);
                var startNode = EntityManager.GetComponentData<Node>(edge.m_Start);
                var distanceToStart = math.distance(hit.m_Position, startNode.m_Position);
                var endNode = EntityManager.GetComponentData<Node>(edge.m_End);
                var distanceToEnd = math.distance(hit.m_Position, endNode.m_Position);

                if (distanceToStart < MaxDistanceToSelect && distanceToStart < distanceToEnd) {
                    candidateEntity = edge.m_Start;
                } else if (distanceToEnd < MaxDistanceToSelect && distanceToEnd < distanceToStart) {
                    candidateEntity = edge.m_End;
                }
            } else {
                candidateEntity = entity;
            }

            // Check eligibility
            if (EntityManager.HasComponent<NT_Eligible>(candidateEntity)) {
                controlPoint = new ControlPoint(candidateEntity, hit);
            }

            return controlPoint;
        }

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
    }
}
