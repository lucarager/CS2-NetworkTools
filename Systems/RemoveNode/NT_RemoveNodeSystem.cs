// <copyright file="NT_CEToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
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
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// # Remove Node System
    /// </summary>
    public partial class NT_RemoveNodeSystem : NT_BaseToolSystem {
        /// <summary>
        /// Maximum distance to select a node when selecting near an edge
        /// </summary>
        private const float MaxDistanceToSelect = 16f;

        private TerrainSystem       m_TerrainSystem;
        private OverlayRenderSystem m_OverlayRenderSystem;

        private EntityQuery m_DefinitionQuery;
        private EntityQuery m_NodesWithEligibleQuery;
        private EntityQuery m_NodesWithHighlightedQuery;
        private EntityQuery m_NodesWithoutEligibleQuery;
        
        /// <summary>
        /// Apply action (usually left click)
        /// </summary>
        private IProxyAction m_ApplyAction;

        /// <summary>
        /// Secondary apply action (usually right click)
        /// </summary>
        private IProxyAction m_SecondaryApplyAction;

        /// <summary>
        /// Caches the last hovered entity to detect changes
        /// </summary>
        private NativeReference<Entity> m_LastHoveredEntity;

        /// <summary>
        /// Caches the last raycast entity to detect changes
        /// </summary>
        private NativeReference<Entity> m_LastRaycastEntity;

        /// <summary>
        /// Current operation state tracking configuration and phase.
        /// </summary>
        private OperationState m_OperationState;

        /// <summary>
        /// Selected Prefab, for this tool this is coming from the UI
        /// </summary>
        private PrefabBase m_Prefab;

        /// <summary>
        /// Tool barrier for command buffers
        /// </summary>
        private ToolOutputBarrier m_Barrier;

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            UpdateActions();

            // Get raycast result
            if (GetRaycastResult(out var controlPoint)) {
                // We hit something
                var newEntityWasHit = m_LastHoveredEntity.Value != controlPoint.m_OriginalEntity;

                if (newEntityWasHit) {
                    HandleHover(controlPoint);
                }

                // Update Cache
                m_LastHoveredEntity.Value = controlPoint.m_OriginalEntity;

                // Handle clicking
                if (m_ApplyAction.WasPressedThisFrame()) {
                    HandleApply(controlPoint.m_OriginalEntity);
                }
            } else {
                // No entity under cursor
                HandleNoHover();
            }

            // Handle temp entities
            return HandleTempEntities(inputDeps);
        }

        private void HandleApply(Entity controlPointEntity) {

        }

        /// <summary>
        /// Runs various jobs depending on whether we need to Update, Apply, or Cancel temp entities
        /// </summary>
        /// <param name="inputDeps"></param>
        /// <returns>inputDeps</returns>
        private JobHandle HandleTempEntities(JobHandle inputDeps) {
            return m_OperationState.Phase switch
            {
                // No temp entities needed
                OperationPhase.Idle or OperationPhase.Configuring => inputDeps,
                // Preview temp entities
                OperationPhase.Ready => Update(inputDeps),
                // Apply real entities
                OperationPhase.Applying => Apply(inputDeps),
                // Clear otherwise
                _ => Clear(inputDeps),
            };
        }

        private void HandleNoHover() {
            m_LastHoveredEntity.Value = Entity.Null;
            ClearAllHighlights();
        }

        private void UpdateActions() {
            m_ApplyAction.shouldBeEnabled          = true;
        }

        private void HandleHover(ControlPoint controlPoint) {
            SwapHighlitedEntities(m_LastHoveredEntity.Value, controlPoint.m_OriginalEntity);
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

            // If we hit an edge, find the closest node instead
            if (EntityManager.HasComponent<Edge>(entity)) {
                // todo make job
                // Find the closest node to the hit position
                var edge            = EntityManager.GetComponentData<Edge>(entity);
                var startNode       = EntityManager.GetComponentData<Node>(edge.m_Start);
                var distanceToStart = math.distance(hit.m_Position, startNode.m_Position);
                var endNode         = EntityManager.GetComponentData<Node>(edge.m_End);
                var distanceToEnd   = math.distance(hit.m_Position, endNode.m_Position);

                if (distanceToStart < MaxDistanceToSelect && distanceToStart < distanceToEnd) {
                    candidateEntity = edge.m_Start;
                } else if (distanceToEnd < MaxDistanceToSelect && distanceToEnd < distanceToStart) {
                    candidateEntity = edge.m_End;
                }
            } else {
                candidateEntity = entity;
            }

            // Check that the entity we're hitting is eligible
            if (EntityManager.HasComponent<NT_Eligible>(candidateEntity)) {
                controlPoint = new ControlPoint(candidateEntity, hit);
            }

            return controlPoint;
        }

        /// <summary>
        /// Swaps highlighting between two entities (removes from old, adds to new).
        /// Simple single-node highlighting utility.
        /// </summary>
        /// <param name="oldEntity">Entity to remove highlighting from</param>
        /// <param name="newEntity">Entity to add highlighting to</param>
        private void SwapHighlitedEntities(Entity oldEntity, Entity newEntity) {
            RemoveHighlight(oldEntity);
            AddHighlight(newEntity);
        }

        private void AddHighlight(Entity entity) { EntityManager.AddComponent<NT_Highlighted>(entity); }

        private void RemoveHighlight(Entity entity) { EntityManager.RemoveComponent<NT_Highlighted>(entity); }

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

        /// <summary>
        /// Clears all NT_Highlighted components from nodes and edges (batch operation).
        /// </summary>
        private void ClearAllHighlights() {
            EntityManager.RemoveComponent<NT_Highlighted>(m_NodesWithHighlightedQuery);
        }
    }
}