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

    using NetworkTools.Components;
    using NetworkTools.Systems.Tools;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    ///     Represents the current selection state of path-based tools.
    /// </summary>
    public enum SuperNodeSelectionState {
        NoSelection = 0,
        StartNodeSelected = 1,
    }

    /// <summary>
    /// # Super Node System
    /// </summary>
    public partial class NT_SuperNodeToolSystem : NT_BaseToolSystem, IManualApplyProvider, INodeSelectionProvider {
        private         bool   m_UpdateNeeded;
        public override string toolID => "SuperNode Tool";
        
        protected NativeList<Entity> m_SelectedNodes;

        /// <summary>
        ///     Gets the array of user-selected node entities (path endpoints).
        /// </summary>
        /// <returns>Array of selected Entity objects.</returns>
        public Entity[] GetSelectedNodes() {
            return m_SelectedNodes.ToArray(Allocator.Temp).ToArray();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            UpdateActions();

            var rightClickPressed = m_SecondaryApplyAction.WasPressedThisFrame();
            var leftClickPressed = m_ApplyAction.WasPressedThisFrame();
            var raycastHit = false;
            var hoveredEntity = Entity.Null;
            var hitPosition = float3.zero;
            ControlPoint controlPoint = default;

            raycastHit = GetRaycastResult(out controlPoint);
            if (raycastHit)
            {
                hoveredEntity = controlPoint.m_OriginalEntity;
                hitPosition = controlPoint.m_HitPosition;
            }

            // Right-click: cancel/back (skips all raycast processing)
            if (rightClickPressed)
            {
                HandleCancel();
                m_UpdateNeeded = true;
            } // Raycast-based interactions
            else if (raycastHit)
            {
                // Update hover state first
                var newEntityHovered = (hoveredEntity != m_LastHoveredEntity.Value);
                if (newEntityHovered)
                {
                    HandleHover(controlPoint);
                }
                m_LastHoveredEntity.Value = hoveredEntity;

                // Left-click: add node 
                if (leftClickPressed && hoveredEntity != Entity.Null)
                {
                    HandleAddNode(hoveredEntity);
                    m_UpdateNeeded = true;
                }
            }
            // No raycast hit
            else
            {
                HandleNoHover();
            }

            // Handle temp entities
            return HandleTempEntities(inputDeps);
        }


        /// <summary>
        ///     Requests the tool to apply the current transformation.
        /// </summary>
        public void RequestApply() {
            m_Log.Debug($"RequestApply() -- Selected Nodes: {m_SelectedNodes.Length}");

            if (m_SelectedNodes.Length < 2)
            {
                return;
            }

            Phase = OperationPhase.Applying;
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
            if (entity == Entity.Null || m_SelectedNodes.Contains(entity))
            {
                return false;
            }

            m_Log.Debug($"Adding node: {entity}");

            // Add node to selection and mark with state-specific components
            m_SelectedNodes.Add(entity);
            EntityManager.AddComponentData(entity, NT_Selected.ForNode(NodeRenderMode.RenderSelected | NodeRenderMode.RenderAsCircle));

            UpdatePhase();

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

            return true;
        }

        /// <summary>
        /// Runs various jobs depending on whether we need to Update, Apply, or Cancel temp entities.
        /// For the remove node tool, we show preview when hovering over a valid node.
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
                _ => Clear(inputDeps)
            };
        }

        private void HandleNoHover() {
            m_LastHoveredEntity.Value = Entity.Null;
            ClearAllHighlights();
        }

        private void HandleHover(ControlPoint controlPoint) {
            SwapHighlightedEntities(m_LastHoveredEntity.Value, controlPoint.m_OriginalEntity, NT_Highlighted.DefaultNode);
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
            if (EntityManager.HasComponent<NT_Eligible>(candidateEntity)) {
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