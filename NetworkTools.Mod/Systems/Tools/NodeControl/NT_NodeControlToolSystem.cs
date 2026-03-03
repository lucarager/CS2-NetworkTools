// <copyright file="NT_NodeControlToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using NetworkTools.Components;
    using NetworkTools.Components.Handles;

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

        /// <summary>
        /// Override to enable handle raycasting only when a node is selected.
        /// </summary>
        protected override bool ShouldRaycastHandles =>
            CurrentSelectionState == NodeControlSelectionState.NodeSelected &&
            m_Handles.IsCreated && m_Handles.Length > 0;

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            UpdateActions();

            // Right click => Cancel / Deselect
            if (m_SecondaryApplyAction.WasPressedThisFrame()) {
                CancelHandleInteraction();
                HandleCancel();
                return inputDeps;
            }

            // When node is selected, use centralized handle input processing
            if (CurrentSelectionState == NodeControlSelectionState.NodeSelected) {
                if (ProcessHandleInput()) {
                    return inputDeps;
                }
            }

            // Handle node selection (NoSelection state)
            if (CurrentSelectionState == NodeControlSelectionState.NoSelection) {
                ProcessNodeSelectionInput();
            }

            return inputDeps;
        }

        /// <summary>
        /// Processes input for node selection in NoSelection state.
        /// </summary>
        private void ProcessNodeSelectionInput() {
            var hasHit = GetRaycastResult(out var controlPoint);
            var hitEntity = hasHit ? controlPoint.m_OriginalEntity : Entity.Null;

            if (hasHit) {
                // Update hover highlight
                if (m_LastHoveredEntity.Value != hitEntity) {
                    HandleHover(controlPoint);
                    m_LastHoveredEntity.Value = hitEntity;
                }

                // Click to select
                if (m_ApplyAction.WasPressedThisFrame() &&
                    hitEntity != Entity.Null &&
                    EntityManager.HasComponent<NT_Eligible>(hitEntity)) {
                    m_Log.Debug("[NoSelection -> NodeSelected] Selecting node.");
                    SelectNode(hitEntity);
                }
            } else {
                HandleNoHover();
            }
        }

        #region Handle Virtual Hook Overrides

        /// <summary>
        /// Called while dragging a handle. Applies the handle position to the bezier curve.
        /// </summary>
        protected override void OnHandleDragging(Entity handle) {
            ApplyHandlePositionToCurve(handle);
        }

        /// <summary>
        /// Called when a handle drag ends.
        /// </summary>
        protected override void OnHandleDragEnd(Entity handle) {
            m_Log.Debug("[OnHandleDragEnd] Drag ended");
        }

        /// <summary>
        /// Called when a handle is clicked (not dragged).
        /// </summary>
        protected override void OnHandleClick(Entity handle) {
            m_Log.Debug("[OnHandleClick] Handle clicked");
        }

        #endregion

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
            m_Log.Debug("[NoSelection] Hovering over potential node.");
            SwapHighlightedEntities(m_LastHoveredEntity.Value, controlPoint.m_OriginalEntity, NT_Highlighted.DefaultNode);
        }

        private void HandleNoHover() {
            // Remove highlight from the last hovered entity directly (handles handles which aren't in m_NodesWithHighlightedQuery)
            if (m_LastHoveredEntity.Value != Entity.Null &&
                EntityManager.HasComponent<NT_Highlighted>(m_LastHoveredEntity.Value)) {
                EntityManager.RemoveComponent<NT_Highlighted>(m_LastHoveredEntity.Value);
            }
            m_LastHoveredEntity.Value = Entity.Null;
            ClearAllHighlights();
        }

        private void SelectNode(Entity entity) {
            // Store the selected node
            m_SelectedNode.Value = entity;

            // Add handle component
            EntityManager.AddComponentData(entity, NT_Selected.DefaultNode);

            // Clear highlights
            ClearAllHighlights();

            // Remove NT_Eligible from ALL nodes
            EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);

            // Generate handle entities using base class method
            CreateNodeHandles(entity);
        }

        /// <summary>
        /// Creates bezier control point handles for all edges connected to a node.
        /// Uses the base class CreatePositionHandle method.
        /// </summary>
        private void CreateNodeHandles(Entity node) {
            var connectedEdges = EntityManager.GetBuffer<ConnectedEdge>(node);

            for (var i = 0; i < connectedEdges.Length; i++) {
                var edgeEntity = connectedEdges[i].m_Edge;
                var edge = EntityManager.GetComponentData<Edge>(edgeEntity);
                var curve = EntityManager.GetComponentData<Curve>(edgeEntity);
                var isForward = edge.m_Start == node;

                // Endpoint handle (a or d)
                var endpointFlags = HandleTypeFlags.BezierPoint | HandleTypeFlags.Primary |
                                    (isForward ? HandleTypeFlags.BezierStartPoint : HandleTypeFlags.BezierEndPoint);
                CreatePositionHandle(
                    node, edgeEntity,
                    isForward ? 0 : 3,
                    isForward ? curve.m_Bezier.a : curve.m_Bezier.d,
                    endpointFlags);

                // Control point handle (b or c)
                var controlFlags = HandleTypeFlags.BezierPoint | HandleTypeFlags.BezierControlPoint | HandleTypeFlags.Secondary;
                CreatePositionHandle(
                    node, edgeEntity,
                    isForward ? 1 : 2,
                    isForward ? curve.m_Bezier.b : curve.m_Bezier.c,
                    controlFlags);
            }
        }

        /// <summary>
        /// Applies the handle's current position to the bezier curve it controls.
        /// Updates the corresponding control point (a, b, c, or d) based on the handle's key.
        /// </summary>
        private void ApplyHandlePositionToCurve(Entity handleEntity) {
            if (!EntityManager.Exists(handleEntity)) return;
            if (!EntityManager.HasComponent<NT_HandleLink>(handleEntity)) return;
            if (!EntityManager.HasComponent<NT_HandlePosition>(handleEntity)) return;

            var handleLink = EntityManager.GetComponentData<NT_HandleLink>(handleEntity);
            var handlePos = EntityManager.GetComponentData<NT_HandlePosition>(handleEntity).Position;
            var edgeEntity = handleLink.LinkedEdge;
            var key = handleLink.Key;

            if (!EntityManager.Exists(edgeEntity)) {
                m_Log.Warn($"[ApplyHandlePositionToCurve] Edge entity {edgeEntity} does not exist");
                return;
            }

            // Get the current curve
            var curve = EntityManager.GetComponentData<Curve>(edgeEntity);
            var bezier = curve.m_Bezier;

            // Update the appropriate control point based on key
            switch (key) {
                case 0:
                    bezier.a = handlePos;
                    break;
                case 1:
                    bezier.b = handlePos;
                    break;
                case 2:
                    bezier.c = handlePos;
                    break;
                case 3:
                    bezier.d = handlePos;
                    break;
                default:
                    m_Log.Warn($"[ApplyHandlePositionToCurve] Invalid key {key}");
                    return;
            }

            // Apply the updated curve
            curve.m_Bezier = bezier;
            EntityManager.SetComponentData(edgeEntity, curve);

            // Mark edge as updated so the game recalculates
            if (!EntityManager.HasComponent<Updated>(edgeEntity)) {
                EntityManager.AddComponent<Updated>(edgeEntity);
            }

            m_Log.Debug($"[ApplyHandlePositionToCurve] Updated bezier point {key} to {handlePos}");
        }

        private void DeselectCurrentNode() {
            var node = m_SelectedNode.Value;
            if (node != Entity.Null) {
                EntityManager.RemoveComponent<NT_Selected>(node);
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
            EntityManager.AddComponent<NT_Eligible>(m_NodesWithoutEligibleQuery);

            // Remove handles using base class method
            DestroyAllHandles();
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
            if (EntityManager.HasComponent<NT_Eligible>(candidateEntity)) {
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
            // Remove selected handle
            EntityManager.RemoveComponent<NT_Selected>(m_NodesWithSelectedQuery);

            // Clear highlights
            EntityManager.RemoveComponent<NT_Highlighted>(m_NodesWithHighlightedQuery);

            // Reset to no selection state
            StateTransitionNoSelection();
        }
    }
}
