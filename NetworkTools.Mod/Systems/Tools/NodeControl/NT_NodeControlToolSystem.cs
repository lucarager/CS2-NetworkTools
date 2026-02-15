// <copyright file="NT_NodeControlToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Game.Common;
    using Game.Input;
    using Game.Net;
    using Game.Notifications;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using NetworkTools.Systems.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// Represents the selection state for the Node Control tool.
    /// </summary>
    public enum NodeControlSelectionState {
        NoSelection  = 0,
        NodeSelected = 1,
    }

    /// <summary>
    /// # Node Control Tool System
    /// 
    /// Simple selection system that allows selecting a single network node.
    /// 
    /// ## State Machine:
    /// 
    /// ### NoSelection
    /// - All network nodes in the game have NT_Eligible component
    /// - Actions:
    ///     - [Hover] over NT_Eligible Node: Clear NT_Highlighted. Adds NT_Highlighted to node.
    ///     - [Hover] over nothing: Removes all NT_Highlighted.
    ///     - [Apply]: Transition to `NodeSelected` with node.
    ///     - [Cancel]: Exit Tool
    /// 
    /// ### NodeSelected
    /// - Selected node is stored
    /// - Selected node has: NT_Selected
    /// - All other nodes are not eligible (no NT_Eligible component)
    /// - Actions:
    ///     - [Hover]: Nothing.
    ///     - [Apply]: Nothing.
    ///     - [Cancel]: Deselect node and return to NoSelection state
    /// </summary>
    public partial class NT_NodeControlToolSystem : NT_BaseToolSystem {
        public override string toolID => "NodeControl Tool";

        private EntityQuery m_NodesWithSelectedQuery;

        private NT_PrefabsCreateSystem m_NTPrefabsCreateSystem;

        /// <summary>
        /// Tracks whether an update/re-render is needed on the next frame.
        /// This is set to true when something changes that requires regenerating preview entities.
        /// Gets reset to false after being processed.
        /// </summary>
        private bool m_UpdateNeeded;

        /// <summary>
        /// Currently selected node entity
        /// </summary>
        private NativeReference<Entity> m_SelectedNode;

        /// <summary>
        /// Current selection state
        /// </summary>
        public NodeControlSelectionState CurrentSelectionState =>
            m_SelectedNode.Value == Entity.Null
                ? NodeControlSelectionState.NoSelection
                : NodeControlSelectionState.NodeSelected;

        /// <summary>
        /// Gets the currently selected node entity.
        /// </summary>
        /// <returns>The selected Entity, or Entity.Null if none selected.</returns>
        public Entity GetSelectedNode() => m_SelectedNode.Value;

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            UpdateActions();

            // Right click => Cancel / Deselect
            if (m_SecondaryApplyAction.WasPressedThisFrame()) {
                HandleCancel();
                return inputDeps;
            }

            // Get raycast result
            if (GetRaycastResult(out var controlPoint)) {
                // We hit something
                var newEntityWasHit = m_LastHoveredEntity.Value != controlPoint.m_OriginalEntity;

                if (newEntityWasHit) {
                    HandleHover(controlPoint);
                }

                // Update Cache
                m_LastHoveredEntity.Value = controlPoint.m_OriginalEntity;

                // Handle dragging (holding apply)
                if (m_ApplyAction.IsPressed()) {
                    HandleDrag(controlPoint);
                }
                // Handle clicking
                else if (m_ApplyAction.WasReleasedThisFrame()) {
                    HandleApply(controlPoint.m_OriginalEntity);
                }
            } else {
                // No entity under cursor
                HandleNoHover();
            }

            return inputDeps;
        }

        private void HandleCancel() {
            switch (CurrentSelectionState) {
                case NodeControlSelectionState.NoSelection:
                    // Exit tool
                    m_Log.Debug("[NoSelection] Cancel pressed, exiting tool.");
                    RequestDisable();
                    break;
                case NodeControlSelectionState.NodeSelected:
                    // Deselect and return to NoSelection
                    m_Log.Debug("[NodeSelected -> NoSelection] Cancel pressed, deselecting node.");
                    DeselectCurrentNode();
                    StateTransitionNoSelection();
                    break;
            }
        }

        private void HandleDrag(ControlPoint controlPoint) {
            switch (CurrentSelectionState) {
                case NodeControlSelectionState.NoSelection:
                    break;
                case NodeControlSelectionState.NodeSelected:
                    MoveMarker(controlPoint.m_OriginalEntity, controlPoint.m_HitPosition);
                    m_Log.Debug("[NodeSelected] Dragging.");
                    break;
            }
        }

        private void HandleApply(Entity entity) {
            switch (CurrentSelectionState) {
                case NodeControlSelectionState.NoSelection:
                    if (entity != Entity.Null) {
                        m_Log.Debug("[NoSelection -> NodeSelected] Selecting node.");
                        SelectNode(entity);
                    }
                    break;
                case NodeControlSelectionState.NodeSelected:
                    // Do nothing when already selected
                    m_Log.Debug("[NodeSelected] Apply pressed, but node already selected.");
                    break;
            }
        }

        private void HandleHover(ControlPoint controlPoint) {
            switch (CurrentSelectionState) {
                case NodeControlSelectionState.NoSelection:
                    m_Log.Debug("[NoSelection] Hovering over potential node.");
                    SwapHighlightedEntities(m_LastHoveredEntity.Value, controlPoint.m_OriginalEntity);
                    break;
                case NodeControlSelectionState.NodeSelected:
                    m_Log.Debug("[NodeSelected] Hovering over potential marker.");
                    SwapHighlightedEntities(m_LastHoveredEntity.Value, controlPoint.m_OriginalEntity);
                    break;
            }
        }

        private void HandleNoHover() {
            m_LastHoveredEntity.Value = Entity.Null;
            ClearAllHighlights();
        }

        private void SelectNode(Entity entity) {
            // Store the selected node
            m_SelectedNode.Value = entity;

            // Add marker component
            EntityManager.AddComponentData(entity, Components.NT_Selected.DefaultNode);

            // Clear highlights
            ClearAllHighlights();

            // Remove NT_Eligible from ALL nodes
            EntityManager.RemoveComponent<Components.NT_Eligible>(m_NodesWithEligibleQuery);

            // Generate marker entities
            CreateMarkers(entity);
        }

        private NativeList<Entity> m_Markers;

        private void CreateMarkers(Entity node) {
            var connectedEdges = EntityManager.GetBuffer<ConnectedEdge>(node);

            for (var i = 0; i < connectedEdges.Length; i++) {
                var edgeEntity = connectedEdges[i].m_Edge;
                var edge = EntityManager.GetComponentData<Edge>(edgeEntity);
                var curve = EntityManager.GetComponentData<Curve>(edgeEntity);
                var isForward = edge.m_Start == node;

                m_Markers.Add(CreateMarker(node, isForward ? 0 : 3, isForward ? curve.m_Bezier.a : curve.m_Bezier.d));
                m_Markers.Add(CreateMarker(node, isForward ? 1 : 2, isForward ? curve.m_Bezier.b : curve.m_Bezier.c));
            }
        }

        /// <summary>
        /// Method that creates Unity ECS entities 
        /// </summary>
        private Entity CreateMarker(Entity linkedEntity, int key, float3 position) {
            var marker = EntityManager.CreateEntity();
            EntityManager.AddComponentData(marker, new Components.NT_Marker());
            EntityManager.AddComponentData(marker, new Components.NT_MarkerLink {
                LinkedEntity = linkedEntity,
                Key = key,
            });
            EntityManager.AddComponentData(marker, new Components.NT_MarkerPosition {
                Position = position,
            });
            return marker;
        }


        private void DestroyMarkers() {
            for (var i = 0; i < m_Markers.Length; i++) {
                var marker = m_Markers[i];
                if (EntityManager.Exists(marker)) {
                    EntityManager.DestroyEntity(marker);
                }
            }
            m_Markers.Clear();
        }

        private void MoveMarker(Entity markerEntity, float3 position) {
            if (EntityManager.Exists(markerEntity)) {
                EntityManager.SetComponentData(markerEntity, new Components.NT_MarkerPosition {
                    Position = position
                });
            }
        }

        private void DeselectCurrentNode() {
            var node = m_SelectedNode.Value;
            if (node != Entity.Null) {
                EntityManager.RemoveComponent<Components.NT_Selected>(node);
                m_SelectedNode.Value = Entity.Null;
            }
        }

        /// <summary>
        /// Transitions to NoSelection state.
        /// Sets all nodes in the game as eligible for selection.
        /// </summary>
        private void StateTransitionNoSelection() {
            m_Log.Debug("StateTransitionNoSelection()");

            // Clear selected node reference
            m_SelectedNode.Value = Entity.Null;

            // Add NT_Eligible to ALL nodes
            EntityManager.AddComponent<Components.NT_Eligible>(m_NodesWithoutEligibleQuery);

            // Remove markers
            DestroyMarkers();
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

            // If we're in the NodeSelected state, we want to check if we're hitting a marker first before checking for edges or terrain
            if (CurrentSelectionState == NodeControlSelectionState.NodeSelected) {
                var minDistance = 2f; // Minimum distance to consider hitting a marker

                for (var i = 0; i < m_Markers.Length; i++) {
                    var markerEntity = m_Markers[i];
                    var markerPosition = EntityManager.GetComponentData<Components.NT_MarkerPosition>(markerEntity).Position;
                    var distanceToMarker = math.distance(hit.m_Position, markerPosition);
                    if (distanceToMarker < minDistance) {
                        candidateEntity = markerEntity;
                        break;
                    }
                }

                m_Log.Debug("[FilterRaycastResult] Marker check: " + (candidateEntity != Entity.Null ? "Hit" : "Miss"));

                return candidateEntity != Entity.Null
                    ? new ControlPoint(candidateEntity, hit)
                    : default;
            } 

            // Ignore "terrain" entity
            if (EntityManager.HasComponent<Terrain>(entity)) {
                m_Log.Debug("[FilterRaycastResult] Terrain, exiting");

                return default;
            }

            if (EntityManager.HasComponent<Edge>(entity)) {
                // If we hit an edge, find the closest node instead
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
            if (EntityManager.HasComponent<Components.NT_Eligible>(candidateEntity)) {
                m_Log.Debug("[FilterRaycastResult] Found eligible component.");
                controlPoint = new ControlPoint(candidateEntity, hit);
            }

            return controlPoint;
        }

        public override void InitializeRaycast() {
            base.InitializeRaycast();

            m_ToolRaycastSystem.collisionMask   = CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.Underground;
            m_ToolRaycastSystem.typeMask        = TypeMask.Terrain | TypeMask.Net;
            m_ToolRaycastSystem.netLayerMask    = Layer.All;
            m_ToolRaycastSystem.iconLayerMask   = IconLayerMask.None;
            m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;
            m_ToolRaycastSystem.raycastFlags    = RaycastFlags.BuildingLots;
        }

        /// <summary>
        /// Resets the tool to idle state, clearing all selections.
        /// </summary>
        public void ResetToIdle() {
            // Remove selected marker
            EntityManager.RemoveComponent<Components.NT_Selected>(m_NodesWithSelectedQuery);

            // Clear highlights
            EntityManager.RemoveComponent<Components.NT_Highlighted>(m_NodesWithHighlightedQuery);

            // Reset to no selection state
            StateTransitionNoSelection();
        }
    }
}
