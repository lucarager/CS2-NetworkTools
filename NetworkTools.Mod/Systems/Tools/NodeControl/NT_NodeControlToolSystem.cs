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
        /// Radius of the invisible sphere around each handle for ray intersection hit detection.
        /// </summary>
        private const float HandleHitRadius = 2f;

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
                    // Only allow dragging handles in NodeSelected state
                    if (CurrentSelectionState == NodeControlSelectionState.NodeSelected &&
                        EntityManager.HasComponent<Components.NT_Handle>(m_MouseDownEntity)) {
                        m_InputState = InputInteractionState.Dragging;
                        m_Log.Debug("[PendingAction -> Dragging] Drag started");

                        // Clear hover highlight and mark handle as selected
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
                // Drag ended - remove selected state from handle
                m_Log.Debug("[Dragging -> Idle] Drag ended");
                if (EntityManager.HasComponent<Components.NT_Selected>(m_MouseDownEntity)) {
                    EntityManager.RemoveComponent<Components.NT_Selected>(m_MouseDownEntity);
                }
                m_InputState = InputInteractionState.Idle;
                return;
            }

            // Continue dragging - project mouse onto XZ plane at handle's Y
            UpdateHandleDragPosition(m_MouseDownEntity);

            // Live preview - apply handle position to curve
            ApplyHandlePositionToCurve(m_MouseDownEntity);
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
                    // Click on handle or elsewhere - could add behavior here
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
                    m_Log.Debug("[NodeSelected] Hovering over potential handle.");
                    SwapHighlightedEntities(m_LastHoveredEntity.Value, controlPoint.m_OriginalEntity, Components.NT_Highlighted.DefaultNode);
                    break;
            }
        }

        private void HandleNoHover() {
            // Remove highlight from the last hovered entity directly (handles handles which aren't in m_NodesWithHighlightedQuery)
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

            // Add handle component
            EntityManager.AddComponentData(entity, Components.NT_Selected.DefaultNode);

            // Clear highlights
            ClearAllHighlights();

            // Remove NT_Eligible from ALL nodes
            EntityManager.RemoveComponent<Components.NT_Eligible>(m_NodesWithEligibleQuery);

            // Generate handle entities
            CreateHandles(entity);
        }

        private NativeList<Entity> m_Handles;

        private void CreateHandles(Entity node) {
            var connectedEdges = EntityManager.GetBuffer<ConnectedEdge>(node);

            for (var i = 0; i < connectedEdges.Length; i++) {
                var edgeEntity = connectedEdges[i].m_Edge;
                var edge = EntityManager.GetComponentData<Edge>(edgeEntity);
                var curve = EntityManager.GetComponentData<Curve>(edgeEntity);
                var isForward = edge.m_Start == node;

                // Endpoint handle (a or d)
                var endpointFlags = Components.HandleTypeFlags.BezierPoint |
                                    (isForward ? Components.HandleTypeFlags.BezierStartPoint : Components.HandleTypeFlags.BezierEndPoint);
                m_Handles.Add(CreateHandle(node, edgeEntity, isForward ? 0 : 3, isForward ? curve.m_Bezier.a : curve.m_Bezier.d, endpointFlags));

                // Control point handle (b or c)
                var controlFlags = Components.HandleTypeFlags.BezierPoint | Components.HandleTypeFlags.BezierControlPoint;
                m_Handles.Add(CreateHandle(node, edgeEntity, isForward ? 1 : 2, isForward ? curve.m_Bezier.b : curve.m_Bezier.c, controlFlags));
            }
        }

        /// <summary>
        /// Creates a handle entity with the specified link data, position, and type flags.
        /// </summary>
        private Entity CreateHandle(Entity linkedEntity, Entity linkedEdge, int key, float3 position, Components.HandleTypeFlags typeFlags) {
            var handle = EntityManager.CreateEntity();
            EntityManager.AddComponentData(handle, new Components.NT_Handle {
                TypeFlags = typeFlags,
            });
            EntityManager.AddComponentData(handle, new Components.NT_HandleLink {
                LinkedEntity = linkedEntity,
                LinkedEdge = linkedEdge,
                Key = key,
            });
            EntityManager.AddComponentData(handle, new Components.NT_HandlePosition {
                Position = position,
            });
            return handle;
        }


        private void DestroyHandles() {
            for (var i = 0; i < m_Handles.Length; i++) {
                var handle = m_Handles[i];
                if (EntityManager.Exists(handle)) {
                    EntityManager.DestroyEntity(handle);
                }
            }
            m_Handles.Clear();
        }

        /// <summary>
        /// Updates the handle position by projecting mouse onto a horizontal plane at the handle's Y.
        /// </summary>
        private void UpdateHandleDragPosition(Entity handleEntity) {
            if (!EntityManager.Exists(handleEntity)) return;
            if (!EntityManager.HasComponent<Components.NT_HandlePosition>(handleEntity)) return;

            var currentPos = EntityManager.GetComponentData<Components.NT_HandlePosition>(handleEntity).Position;
            var fixedY = currentPos.y;

            if (TryGetXZPlaneIntersection(fixedY, out var intersection)) {
                EntityManager.SetComponentData(handleEntity, new Components.NT_HandlePosition {
                    Position = intersection
                });
            }
        }

        /// <summary>
        /// Applies the handle's current position to the bezier curve it controls.
        /// Updates the corresponding control point (a, b, c, or d) based on the handle's key.
        /// </summary>
        private void ApplyHandlePositionToCurve(Entity handleEntity) {
            if (!EntityManager.Exists(handleEntity)) return;
            if (!EntityManager.HasComponent<Components.NT_HandleLink>(handleEntity)) return;
            if (!EntityManager.HasComponent<Components.NT_HandlePosition>(handleEntity)) return;

            var handleLink = EntityManager.GetComponentData<Components.NT_HandleLink>(handleEntity);
            var handlePos = EntityManager.GetComponentData<Components.NT_HandlePosition>(handleEntity).Position;
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

        /// <summary>
        /// Gets the closest handle entity that the camera ray intersects, treating handles as spheres.
        /// </summary>
        /// <param name="handleRadius">The radius of the invisible sphere around each handle.</param>
        /// <returns>The closest handle entity hit, or Entity.Null if none.</returns>
        private Entity GetClosestHandleFromRay(float handleRadius) {
            var camera = Camera.main;
            if (camera == null) return Entity.Null;

            var mousePos = Mouse.current.position.ReadValue();
            var ray = camera.ScreenPointToRay(mousePos);
            var rayOrigin = (float3)ray.origin;
            var rayDir = math.normalize((float3)ray.direction);

            var closestHandle = Entity.Null;
            var closestT = float.MaxValue;

            for (var i = 0; i < m_Handles.Length; i++) {
                var handleEntity = m_Handles[i];
                var handlePos = EntityManager.GetComponentData<Components.NT_HandlePosition>(handleEntity).Position;

                if (TryRaySphereIntersection(rayOrigin, rayDir, handlePos, handleRadius, out var t)) {
                    if (t < closestT) {
                        closestT = t;
                        closestHandle = handleEntity;
                    }
                }
            }

            return closestHandle;
        }

        /// <summary>
        /// Tests for intersection between a ray and a sphere.
        /// </summary>
        /// <param name="rayOrigin">The origin point of the ray.</param>
        /// <param name="rayDir">The normalized direction of the ray.</param>
        /// <param name="sphereCenter">The center of the sphere.</param>
        /// <param name="radius">The radius of the sphere.</param>
        /// <param name="t">The distance along the ray to the intersection point (if found).</param>
        /// <returns>True if the ray intersects the sphere, false otherwise.</returns>
        private static bool TryRaySphereIntersection(float3 rayOrigin, float3 rayDir, float3 sphereCenter, float radius, out float t) {
            t = 0;

            // Vector from ray origin to sphere center
            var oc = rayOrigin - sphereCenter;

            // Quadratic formula coefficients: at² + bt + c = 0
            var a = math.dot(rayDir, rayDir);
            var b = 2f * math.dot(oc, rayDir);
            var c = math.dot(oc, oc) - radius * radius;

            var discriminant = b * b - 4 * a * c;

            // No intersection if discriminant is negative
            if (discriminant < 0) return false;

            // Find the nearest intersection point in front of the ray origin
            t = (-b - math.sqrt(discriminant)) / (2f * a);

            // If t is negative, the intersection is behind the camera
            return t > 0;
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

            // Remove handles
            DestroyHandles();
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

            // If we're in the NodeSelected state, check for handle hits using ray-sphere intersection
            if (CurrentSelectionState == NodeControlSelectionState.NodeSelected) {
                candidateEntity = GetClosestHandleFromRay(HandleHitRadius);

                m_Log.Debug("[FilterRaycastResult] Handle check: " + (candidateEntity != Entity.Null ? "Hit" : "Miss"));

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
            // Remove selected handle
            EntityManager.RemoveComponent<Components.NT_Selected>(m_NodesWithSelectedQuery);

            // Clear highlights
            EntityManager.RemoveComponent<Components.NT_Highlighted>(m_NodesWithHighlightedQuery);

            // Reset to no selection state
            StateTransitionNoSelection();
        }
    }
}
