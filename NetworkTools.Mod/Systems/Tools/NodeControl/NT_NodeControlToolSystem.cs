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
    using UnityEngine;
    using UnityEngine.InputSystem;

    #endregion

    /// <summary>
    /// Represents the selection state for the Node Control tool.
    /// </summary>
    public enum NodeControlSelectionState {
        NoSelection  = 0,
        NodeSelected = 1,
    }

    /// <summary>
    /// Tracks the current input interaction state (separate from selection state).
    /// </summary>
    public enum InputInteractionState {
        /// <summary>Not pressing anything.</summary>
        Idle = 0,
        /// <summary>Mouse down, but haven't moved enough to determine drag vs click.</summary>
        PendingAction = 1,
        /// <summary>Confirmed drag in progress.</summary>
        Dragging = 2,
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
        /// Current input interaction state.
        /// </summary>
        private InputInteractionState m_InputState;

        /// <summary>
        /// World position when mouse was pressed (for drag threshold detection).
        /// </summary>
        private float3 m_MouseDownPosition;

        /// <summary>
        /// Entity under cursor when mouse was pressed.
        /// </summary>
        private Entity m_MouseDownEntity;

        /// <summary>
        /// World units the mouse must move before being considered a drag.
        /// </summary>
        private const float DragThreshold = 0.5f;

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
                CancelCurrentInteraction();
                HandleCancel();
                return inputDeps;
            }

            // Get raycast result
            var hasHit      = GetRaycastResult(out var controlPoint);
            var hitPosition = hasHit ? controlPoint.m_HitPosition : float3.zero;
            var hitEntity   = hasHit ? controlPoint.m_OriginalEntity : Entity.Null;

            switch (m_InputState) {
                case InputInteractionState.Idle:
                    HandleIdleState(hasHit, hitEntity, controlPoint);
                    break;

                case InputInteractionState.PendingAction:
                    HandlePendingState(hasHit, hitPosition);
                    break;

                case InputInteractionState.Dragging:
                    HandleDraggingState();
                    break;
            }

            return inputDeps;
        }

        private void HandleIdleState(bool hasHit, Entity hitEntity, ControlPoint controlPoint) {
            if (hasHit) {
                // Update hover highlight
                if (m_LastHoveredEntity.Value != hitEntity) {
                    HandleHover(controlPoint);
                    m_LastHoveredEntity.Value = hitEntity;
                }

                // Mouse down - start potential interaction
                if (m_ApplyAction.WasPressedThisFrame()) {
                    m_InputState = InputInteractionState.PendingAction;
                    m_MouseDownPosition = controlPoint.m_HitPosition;
                    m_MouseDownEntity = hitEntity;
                    m_Log.Debug($"[Idle -> PendingAction] Mouse down on {hitEntity}");
                }
            } else {
                HandleNoHover();
            }
        }

        private void HandlePendingState(bool hasHit, float3 hitPosition) {
            if (m_ApplyAction.WasReleasedThisFrame()) {
                // Released before dragging threshold - this is a CLICK
                m_Log.Debug("[PendingAction -> Idle] Click detected");
                HandleClick(m_MouseDownEntity);
                m_InputState = InputInteractionState.Idle;
                return;
            }

            if (!m_ApplyAction.IsPressed()) {
                // Button no longer pressed (edge case)
                m_InputState = InputInteractionState.Idle;
                return;
            }

            // Check if we've moved enough to be considered a drag
            if (hasHit) {
                var distance = math.distance(hitPosition.xz, m_MouseDownPosition.xz);
                if (distance > DragThreshold) {
                    // Only allow dragging markers in NodeSelected state
                    if (CurrentSelectionState == NodeControlSelectionState.NodeSelected &&
                        EntityManager.HasComponent<Components.NT_Marker>(m_MouseDownEntity)) {
                        m_InputState = InputInteractionState.Dragging;
                        m_Log.Debug("[PendingAction -> Dragging] Drag started");

                        // Clear hover highlight and mark marker as selected
                        ClearAllHighlights();
                        EntityManager.AddComponentData(m_MouseDownEntity, Components.NT_Selected.DefaultNode);
                    } else {
                        // Can't drag this entity, cancel the interaction
                        m_InputState = InputInteractionState.Idle;
                    }
                }
            }
        }

        private void HandleDraggingState() {
            if (m_ApplyAction.WasReleasedThisFrame()) {
                // Drag ended - remove selected state from marker
                m_Log.Debug("[Dragging -> Idle] Drag ended");
                if (EntityManager.HasComponent<Components.NT_Selected>(m_MouseDownEntity)) {
                    EntityManager.RemoveComponent<Components.NT_Selected>(m_MouseDownEntity);
                }
                m_InputState = InputInteractionState.Idle;
                return;
            }

            // Continue dragging - project mouse onto XZ plane at marker's Y
            UpdateMarkerDragPosition(m_MouseDownEntity);
        }

        private void HandleClick(Entity entity) {
            switch (CurrentSelectionState) {
                case NodeControlSelectionState.NoSelection:
                    if (entity != Entity.Null && EntityManager.HasComponent<Components.NT_Eligible>(entity)) {
                        m_Log.Debug("[NoSelection -> NodeSelected] Selecting node.");
                        SelectNode(entity);
                    }
                    break;
                case NodeControlSelectionState.NodeSelected:
                    // Click on marker or elsewhere - could add behavior here
                    m_Log.Debug("[NodeSelected] Click registered.");
                    break;
            }
        }

        private void CancelCurrentInteraction() {
            m_InputState = InputInteractionState.Idle;
            m_MouseDownEntity = Entity.Null;
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

        private void HandleHover(ControlPoint controlPoint) {
            switch (CurrentSelectionState) {
                case NodeControlSelectionState.NoSelection:
                    m_Log.Debug("[NoSelection] Hovering over potential node.");
                    SwapHighlightedEntities(m_LastHoveredEntity.Value, controlPoint.m_OriginalEntity, Components.NT_Highlighted.DefaultNode);
                    break;
                case NodeControlSelectionState.NodeSelected:
                    m_Log.Debug("[NodeSelected] Hovering over potential marker.");
                    SwapHighlightedEntities(m_LastHoveredEntity.Value, controlPoint.m_OriginalEntity, Components.NT_Highlighted.DefaultNode);
                    break;
            }
        }

        private void HandleNoHover() {
            // Remove highlight from the last hovered entity directly (handles markers which aren't in m_NodesWithHighlightedQuery)
            if (m_LastHoveredEntity.Value != Entity.Null &&
                EntityManager.HasComponent<Components.NT_Highlighted>(m_LastHoveredEntity.Value)) {
                EntityManager.RemoveComponent<Components.NT_Highlighted>(m_LastHoveredEntity.Value);
            }
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

        /// <summary>
        /// Updates the marker position by projecting mouse onto a horizontal plane at the marker's Y.
        /// </summary>
        private void UpdateMarkerDragPosition(Entity markerEntity) {
            if (!EntityManager.Exists(markerEntity)) return;
            if (!EntityManager.HasComponent<Components.NT_MarkerPosition>(markerEntity)) return;

            var currentPos = EntityManager.GetComponentData<Components.NT_MarkerPosition>(markerEntity).Position;
            var fixedY = currentPos.y;

            if (TryGetXZPlaneIntersection(fixedY, out var intersection)) {
                EntityManager.SetComponentData(markerEntity, new Components.NT_MarkerPosition {
                    Position = intersection
                });
            }
        }

        /// <summary>
        /// Gets the intersection point of the camera ray (through mouse position) with a horizontal plane.
        /// </summary>
        /// <param name="planeY">The Y height of the horizontal plane.</param>
        /// <param name="intersection">The resulting intersection point.</param>
        /// <returns>True if intersection found, false otherwise.</returns>
        private bool TryGetXZPlaneIntersection(float planeY, out float3 intersection) {
            intersection = float3.zero;

            var camera = Camera.main;
            if (camera == null) {
                m_Log.Warn("Camera.main is null");
                return false;
            }

            // Create ray from camera through mouse position
            var mousePos = Mouse.current.position.ReadValue();
            var ray = camera.ScreenPointToRay(mousePos);

            var rayOrigin = (float3)ray.origin;
            var rayDirection = math.normalize((float3)ray.direction);

            // Plane equation: y = planeY (normal is (0, 1, 0))
            // Ray: P = origin + t * direction
            // Solve for t: origin.y + t * direction.y = planeY

            // Avoid division by zero (ray parallel to plane)
            if (math.abs(rayDirection.y) < 0.0001f) {
                return false;
            }

            var t = (planeY - rayOrigin.y) / rayDirection.y;

            // Only consider intersections in front of the camera
            if (t < 0) {
                return false;
            }

            intersection = rayOrigin + t * rayDirection;
            return true;
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
