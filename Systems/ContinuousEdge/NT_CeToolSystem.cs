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
    /// Represents the phase of the current transformation operation.
    /// </summary>
    public enum OperationPhase {
        Idle        = 0, // No operation configured
        Configuring = 1, // Operation configured but insufficient selection (< 2 nodes)
        Ready       = 2, // Operation configured with valid selection (>= 2 nodes), can show preview
        Applying    = 3, // Operation is being applied to real entities
    }

    /// <summary>
    /// Tracks the state of the current transformation operation.
    /// </summary>
    public struct OperationState {
        public OperationPhase   Phase;
        public SlopeCurveConfig Config;

        /// <summary>
        /// Whether this operation can show a preview (has sufficient selection).
        /// </summary>
        public bool CanPreview => Phase == OperationPhase.Ready;

        /// <summary>
        /// Whether this operation is active and configured.
        /// </summary>
        public bool IsActive => Phase != OperationPhase.Idle;

        /// <summary>
        /// Creates an idle state with no operation.
        /// </summary>
        public static OperationState Idle() {
            return new OperationState
            {
                Phase  = OperationPhase.Idle,
                Config = new SlopeCurveConfig(),
            };
        }

        /// <summary>
        /// Creates a new operation state with the given configuration.
        /// </summary>
        public static OperationState Create(SlopeCurveConfig config, int selectedNodeCount) {
            return new OperationState
            {
                Phase  = selectedNodeCount >= 2 ? OperationPhase.Ready : OperationPhase.Configuring,
                Config = config,
            };
        }
    }

    /// <summary>
    /// Represents the currentEntity selection state of the tool.
    /// </summary>
    public enum SelectionState {
        NoSelection       = 0,
        StartNodeSelected = 1,
        EndNodeSelected   = 2,
    }

    /// <summary>
    /// # Continuous Edge (CE) Tool System
    /// 
    /// Selection System that allows selecting a contiguous edge and performing operations on it.
    /// It selects all edge segments between a start and end node.
    /// 
    /// - The *OperationState* tracks the current transformation operation (slope/curve) and its phase.
    /// - The *SelectionState* handles user interactions for selecting nodes and edges. This happens during the `Configuring` phase of the OperationState.
    /// 
    /// </summary>
    public partial class NT_CeToolSystem : NT_BaseToolSystem {
        /// <summary>
        /// Maximum distance to select a node when selecting near an edge
        /// </summary>
        private const float MaxDistanceToSelect = 16f;

        private TerrainSystem       m_TerrainSystem;
        private OverlayRenderSystem m_OverlayRenderSystem;

        private EntityQuery m_DefinitionQuery;
        private EntityQuery m_EdgesWithHighlightedQuery;
        private EntityQuery m_EdgesWithSelectedQuery;
        private EntityQuery m_NodesWithEligibleQuery;
        private EntityQuery m_NodesWithHighlightedQuery;
        private EntityQuery m_NodesWithoutEligibleQuery;
        private EntityQuery m_NodesWithSelectedFirstQuery;
        private EntityQuery m_NodesWithSelectedLastQuery;
        private EntityQuery m_NodesWithSelectedQuery;

        /// <summary>
        /// Caches the last hit position
        /// </summary>
        private float3 m_LastHitPosition;

        /// <summary>
        /// Apply action (usually left click)
        /// </summary>
        private IProxyAction m_ApplyAction;

        /// <summary>
        /// Secondary apply action (usually right click)
        /// </summary>
        private IProxyAction m_SecondaryApplyAction;

        /// <summary>
        /// Currently selected path of edges
        /// </summary>
        private NativeList<Entity> m_CurrentPathEdges;

        /// <summary>
        /// Currently selected path of nodes
        /// </summary>
        private NativeList<Entity> m_CurrentPathNodes;

        /// <summary>
        /// List of currently eligible node entities for selection
        /// </summary>
        private NativeList<Entity> m_EligibleNodes;

        /// <summary>
        /// Next path of edges (updated on hover)
        /// </summary>
        private NativeList<Entity> m_NextPathEdges;

        /// <summary>
        /// Next path of nodes (updated on hover)
        /// </summary>
        private NativeList<Entity> m_NextPathNodes;

        /// <summary>
        /// List of currently selected node entities, creating a contiguous path
        /// </summary>
        private NativeList<Entity> m_SelectedNodes;

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

        /// <summary>
        /// Current selection state (Happens during Configuring phase of OperationState)
        /// 
        /// ## State machine:
        /// 
        /// ### NoSelection
        /// - All network nodes in the game have NT_Eligible component
        /// - Actions:
        ///     - [Hover] over NT_Eligible Node: Clear NT_Highlighted. Adds NT_Highlighted to node.
        ///     - [Hover] over nothing: Removes all NT_Highlighted.
        ///     - [Apply]: Transition to `StartNodeSelected` with node.
        ///     - [Cancel]: Exit Tool
        /// 
        /// ### StartNodeSelected
        /// - When entering state with node, adds this node to the start of the "Nodes" list. This node is now the start node
        /// - First node has: NT_Selected, NT_SelectedFirst
        /// - Eligible nodes are nodes reachable via an uninterrupted edge (no intersections) from the start node.
        /// - Any eligible nodes have: NT_Eligible
        /// - Actions:
        ///     - [Hover] over NT_Eligible Node: Clear NT_Highlighted. Adds NT_Highlighted to node. Add NT_Highlighted to Edges and Nodes between start and hovered node.
        ///     - [Hover] over nothing: Removes all NT_Highlighted.
        ///     - [Apply]: Transition to `EndNodeSelected` with node.
        ///     - [Cancel]: Transition back to `NoSelection`
        /// 
        /// ### EndNodeSelected
        /// - When entering state with node, adds this node to the "Nodes" list. The new node is now the end node.
        /// - First node has: NT_Selected, NT_SelectedFirst
        /// - Last node has: NT_Selected, NT_SelectedLast
        /// - Edges and Nodes in path between the two have: NT_Selected
        /// - Eligible nodes are nodes reachable via an uninterrupted edge (no intersections) from the end node. This allows "extending" the selected edge beyond intersections.
        /// - Any eligible nodes have: NT_Eligible
        /// - Actions:
        ///     - [Hover] over NT_Eligible Node: Clear NT_Highlighted. Adds NT_Highlighted to node. Add NT_Highlighted to Edges and Nodes between currentEntity end node and hovered node.
        ///     - [Hover] over nothing: Removes all NT_Highlighted.
        ///     - [Apply]: Transition to `EndNodeSelected` with new end node.
        ///     - [Cancel]: Pop last node from cache. If it's the last "end node", transition back to `StartNodeSelected`
        /// </summary>
        public SelectionState CurrentState =>
            m_SelectedNodes.Length switch
            {
                0 => SelectionState.NoSelection,
                1 => SelectionState.StartNodeSelected,
                _ => SelectionState.EndNodeSelected,
            };

        /// <summary>
        /// Gets the array of currently selected node entities.
        /// </summary>
        /// <returns>Array of selected Entity objects.</returns>
        public Entity[] GetSelectedNodes() { return m_SelectedNodes.ToArray(Allocator.Temp).ToArray(); }

        /// <summary>
        /// Configures a new transformation operation from the UI.
        /// </summary>
        public void SetTransformationConfig(SlopeCurveConfig config) {
            m_OperationState = OperationState.Create(config, m_SelectedNodes.Length);
            m_Log.Debug($"Transformation configured. Phase={m_OperationState.Phase}");
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            UpdateActions();

            // Right click => Remove last point from stack
            if (m_SecondaryApplyAction.WasPressedThisFrame()) {
                HandleRemoveNode();
                return inputDeps;
            }

            // Get raycast result
            if (GetRaycastResult(out var controlPoint)) {
                // We hit something
                var hitPos          = controlPoint.m_HitPosition;
                var newEntityWasHit = m_LastHoveredEntity.Value != controlPoint.m_OriginalEntity;

                if (newEntityWasHit) {
                    // Calculate the path to the new entity if needed
                    HandlePathUpdate(controlPoint);
                    // Handle hovering if entity changed
                    HandleHover(controlPoint);
                }

                // Update Cache
                m_LastHoveredEntity.Value = controlPoint.m_OriginalEntity;
                m_LastHitPosition         = hitPos;

                // Handle clicking
                if (m_ApplyAction.WasPressedThisFrame()) {
                    HandleAddNode(controlPoint.m_OriginalEntity);
                }
            } else {
                // No entity under cursor
                HandleNoHover();
            }

            // Handle temp entities
            return HandleTempEntities(inputDeps);
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
            m_NextPathNodes.Clear();
            m_NextPathEdges.Clear();
            m_LastHoveredEntity.Value = Entity.Null;
            m_LastHitPosition         = float3.zero;
            ClearAllHighlights();
        }

        private void UpdateActions() {
            m_ApplyAction.shouldBeEnabled          = true;
            m_SecondaryApplyAction.shouldBeEnabled = true;
        }

        private void HandlePathUpdate(ControlPoint controlPoint) {
            if (CurrentState == SelectionState.NoSelection) {
                return;
            }

            var startNode = m_SelectedNodes[^1];
            var endNode   = controlPoint.m_OriginalEntity;

            // Find path from first node to hovered node
            var newPathNodes = new NativeList<Entity>(16, Allocator.Temp);
            var newPathEdges = new NativeList<Entity>(16, Allocator.Temp);
            var newPathFound = FindPathBetween(startNode, endNode, ref newPathNodes, ref newPathEdges);

            if (newPathFound) {
                m_NextPathNodes.Clear();
                m_NextPathNodes.AddRange(newPathNodes.AsArray());
                m_NextPathEdges.Clear();
                m_NextPathEdges.AddRange(newPathEdges.AsArray());
            }

            newPathNodes.Dispose();
            newPathEdges.Dispose();
        }

        private void HandleHover(ControlPoint controlPoint) {
            switch (CurrentState) {
                case SelectionState.NoSelection:
                    m_Log.Debug("[NoSelection] Hovering over potential start point.");
                    SwapHighlitedEntities(m_LastHoveredEntity.Value, controlPoint.m_OriginalEntity);
                    return;
                case SelectionState.StartNodeSelected:
                    PreviewPath();
                    m_Log.Debug("[StartNodeSelected] Hovering over potential end point.");
                    return;
                case SelectionState.EndNodeSelected:
                    PreviewPath();
                    m_Log.Debug("[EndNodeSelected] Hovering over another potential end point.");
                    return;
            }
        }

        public void RequestApply() {
            if (m_OperationState.Phase != OperationPhase.Ready) {
                return;
            }
            m_OperationState.Phase = OperationPhase.Applying;
        }

        private void HandleAddNode(Entity entity) {
            if (entity == Entity.Null || m_SelectedNodes.Contains(entity)) {
                return;
            }

            // Add Node
            switch (CurrentState) {
                case SelectionState.NoSelection:
                    m_Log.Debug("[NoSelection -> StartNodeSelected] Adding start point.");
                    m_SelectedNodes.Add(entity);

                    // Add markers to first node
                    EntityManager.AddComponent<NT_Selected>(entity);
                    EntityManager.AddComponent<NT_SelectedFirst>(entity);
                    break;
                case SelectionState.StartNodeSelected:
                    m_Log.Debug("[StartNodeSelected -> EndNodeSelected] Adding end point.");
                    m_SelectedNodes.Add(entity);

                    // Add markers to end node
                    EntityManager.AddComponent<NT_Selected>(entity);
                    EntityManager.AddComponent<NT_SelectedLast>(entity);

                    break;
                case SelectionState.EndNodeSelected:
                    m_Log.Debug("[EndNodeSelected] Adding another end point.");

                    // Remove marker from previous end node
                    var lastNode = m_SelectedNodes[^1];
                    EntityManager.RemoveComponent<NT_SelectedLast>(lastNode);

                    m_SelectedNodes.Add(entity);

                    // Add markers to new end node
                    EntityManager.AddComponent<NT_Selected>(entity);
                    EntityManager.AddComponent<NT_SelectedLast>(entity);

                    break;
            }

            // Add the nodes to our cache and mark as selected
            foreach (var node in m_NextPathNodes) {
                if (!m_CurrentPathNodes.Contains(node)) {
                    m_CurrentPathNodes.Add(node);
                    EntityManager.AddComponent<NT_Selected>(node);
                }
            }

            // Add the edges to our cache and mark as selected
            foreach (var edge in m_NextPathEdges) {
                if (!m_CurrentPathEdges.Contains(edge)) {
                    m_CurrentPathEdges.Add(edge);
                    EntityManager.AddComponent<NT_Selected>(edge);
                }
            }

            // Update all path indices to ensure they're consecutive and correct
            UpdatePathIndices();

            // Remove NT_Eligible from ALL nodes (we will recalculate based on state)
            EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);

            // Find all eligible nodes from new head of path
            FindEligibleNodes(entity, m_EligibleNodes);

            // Add NT_Eligible to eligible nodes
            var eligibleArray = new NativeArray<Entity>(m_EligibleNodes.AsArray(), Allocator.Temp);
            EntityManager.AddComponent<NT_Eligible>(eligibleArray);
            eligibleArray.Dispose();
        }

        private void HandleRemoveNode() {
            var lastNode = m_SelectedNodes[^1];
            switch (CurrentState) {
                case SelectionState.NoSelection:
                    break;
                case SelectionState.StartNodeSelected:
                    m_Log.Debug("[StartNodeSelected -> NoSelection] Removing start point.");
                    EntityManager.RemoveComponent<NT_Selected>(lastNode);
                    EntityManager.RemoveComponent<NT_SelectedFirst>(lastNode);
                    m_SelectedNodes.RemoveAt(m_SelectedNodes.Length - 1);
                    StateTransitionNoNodes();
                    break;
                case SelectionState.EndNodeSelected:
                    if (m_SelectedNodes.Length > 2) {
                        m_Log.Debug($"[EndNodeSelected] Removing an end point. {m_SelectedNodes.Length - 1} end points remaining");
                    } else {
                        m_Log.Debug("[EndNodeSelected -> StartNodeSelected] Removing last end point.");
                    }

                    EntityManager.RemoveComponent<NT_Selected>(lastNode);
                    EntityManager.RemoveComponent<NT_SelectedLast>(lastNode);
                    m_SelectedNodes.RemoveAt(m_SelectedNodes.Length - 1);

                    // Reduce our path - remove nodes and edges until we reach the new last node
                    var newLastNode = m_SelectedNodes[^1];
                    var done        = false;
                    while (!done) {
                        var curNode = m_CurrentPathNodes[^1];
                        if (curNode == newLastNode || m_CurrentPathNodes.Length == 1) {
                            done = true;
                            break;
                        }

                        EntityManager.RemoveComponent<NT_Selected>(curNode);
                        m_CurrentPathNodes.RemoveAt(m_CurrentPathNodes.Length - 1);

                        // Remove corresponding edge
                        if (m_CurrentPathEdges.Length > 0) {
                            var curEdge = m_CurrentPathEdges[^1];
                            EntityManager.RemoveComponent<NT_Selected>(curEdge);
                            m_CurrentPathEdges.RemoveAt(m_CurrentPathEdges.Length - 1);
                        }
                    }

                    // Update all remaining path indices to be consecutive
                    UpdatePathIndices();

                    if (m_SelectedNodes.Length >= 2) {
                        // Mark the new last node if we still have at least 2
                        EntityManager.AddComponent<NT_SelectedLast>(newLastNode);

                        // Recalculate eligible nodes from new end node
                        EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);
                        FindEligibleNodes(newLastNode, m_EligibleNodes);
                        var eligibleArray = new NativeArray<Entity>(m_EligibleNodes.AsArray(), Allocator.Temp);
                        EntityManager.AddComponent<NT_Eligible>(eligibleArray);
                        eligibleArray.Dispose();
                    }

                    break;
            }
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

        /// <summary>
        /// Updates the PathIndex for all nodes in m_CurrentPathNodes to reflect their position in the path.
        /// This should be called after any add/remove operation to keep indices synchronized.
        /// </summary>
        private void UpdatePathIndices() {
            for (var i = 0; i < m_CurrentPathNodes.Length; i++) {
                var node = m_CurrentPathNodes[i];
                if (EntityManager.HasComponent<NT_Selected>(node)) {
                    EntityManager.SetComponentData(node, new NT_Selected { PathIndex = i });
                }
            }
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

        public void ApplySlopeToSelectedEdges(SlopeCurveConfig curveConfig) {
            var buffer = new EntityCommandBuffer(Allocator.Temp);

            // Validation: At least two nodes selected
            if (m_SelectedNodes.Length < 2) {
                m_Log.Debug("ApplySlopeToSelectedEdges: Not enough nodes selected.");
                return;
            }

            var startNode     = m_SelectedNodes[0];
            var endNode       = m_SelectedNodes[^1];
            var startNodeData = EntityManager.GetComponentData<Node>(startNode);
            var endNodeData   = EntityManager.GetComponentData<Node>(endNode);
            var startHeight   = startNodeData.m_Position.y;
            var endHeight     = endNodeData.m_Position.y;
            var deltaHeight   = endHeight - startHeight;

            m_Log.Debug($"ApplySlopeToSelectedEdges: Using template {curveConfig.Template}");

            // Calculate total length of the path using the edge beziers
            var segmentLengths = new NativeList<float>(m_CurrentPathEdges.Length, Allocator.Temp);
            var totalLength    = 0f;

            foreach (var edgeEntity in m_CurrentPathEdges) {
                if (EntityManager.TryGetComponent<Curve>(edgeEntity, out var curve)) {
                    var segmentLength = curve.m_Length;
                    segmentLengths.Add(segmentLength);
                    totalLength += segmentLength;
                } else {
                    segmentLengths.Add(0f);
                }
            }

            if (totalLength <= 0f) {
                m_Log.Debug("ApplySlopeToSelectedEdges: Total path length is zero.");
                segmentLengths.Dispose();
                buffer.Dispose();
                return;
            }

            // Calculate node heights based on their position along the path
            var nodeHeights       = new NativeArray<float>(m_CurrentPathNodes.Length, Allocator.Temp);
            var distanceAlongPath = 0f;

            // Update each edge bezier with interpolated control point heights
            for (var i = 0; i < m_CurrentPathEdges.Length; i++) {
                var edgeEntity = m_CurrentPathEdges[i];
                var edge       = EntityManager.GetComponentData<Edge>(edgeEntity);

                if (!EntityManager.TryGetComponent<Curve>(edgeEntity, out var curve)) {
                    continue;
                }

                var bezier        = curve.m_Bezier;
                var segmentLength = segmentLengths[i];

                // Calculate parametric positions of control points based on horizontal distance
                var horizontalA = new float3(bezier.a.x, 0f, bezier.a.z);
                var horizontalB = new float3(bezier.b.x, 0f, bezier.b.z);
                var horizontalC = new float3(bezier.c.x, 0f, bezier.c.z);
                var horizontalD = new float3(bezier.d.x, 0f, bezier.d.z);

                var totalHorizontalDist = math.distance(horizontalA, horizontalD);

                // Calculate ratios for control points within the segment
                var bRatio = 1f / 3f;
                var cRatio = 2f / 3f;

                if (totalHorizontalDist > 0.01f) {
                    bRatio = math.distance(horizontalA, horizontalB) / totalHorizontalDist;
                    cRatio = math.distance(horizontalA, horizontalC) / totalHorizontalDist;
                }

                bRatio = math.clamp(bRatio, 0f, 1f);
                cRatio = math.clamp(cRatio, 0f, 1f);

                // Calculate distances along entire path for each bezier point
                var distA = distanceAlongPath;
                var distB = distanceAlongPath + segmentLength * bRatio;
                var distC = distanceAlongPath + segmentLength * cRatio;
                var distD = distanceAlongPath + segmentLength;

                // Calculate ratios along entire path and apply curve
                var ratioA = distA / totalLength;
                var ratioB = distB / totalLength;
                var ratioC = distC / totalLength;
                var ratioD = distD / totalLength;

                var curvedA = curveConfig.ApplyCurve(ratioA);
                var curvedB = curveConfig.ApplyCurve(ratioB);
                var curvedC = curveConfig.ApplyCurve(ratioC);
                var curvedD = curveConfig.ApplyCurve(ratioD);

                // Set heights using curved ratios
                bezier.a.y = startHeight + deltaHeight * curvedA;
                bezier.b.y = startHeight + deltaHeight * curvedB;
                bezier.c.y = startHeight + deltaHeight * curvedC;
                bezier.d.y = startHeight + deltaHeight * curvedD;

                curve.m_Bezier = bezier;
                buffer.SetComponent(edgeEntity, curve);

                m_Log.Debug($"Edge {i}: a.y={bezier.a.y:F2}, b.y={bezier.b.y:F2}, c.y={bezier.c.y:F2}, d.y={bezier.d.y:F2}");

                // Mark nodes as updated
                Node_SetUpdated(buffer, edge.m_Start);
                Node_SetUpdated(buffer, edge.m_End);

                distanceAlongPath += segmentLength;
            }

            buffer.Playback(EntityManager);
            buffer.Dispose();
            segmentLengths.Dispose();
            nodeHeights.Dispose();

            m_Log.Debug(
                $"ApplySlopeToSelectedEdges: Applied {curveConfig.Template} slope from {startHeight:F2} to {endHeight:F2} over {totalLength:F2} units to {m_CurrentPathNodes.Length} nodes and {m_CurrentPathEdges.Length} edges.");
        }

        private void TryAddUpdate(in EntityCommandBuffer buffer, Entity e) {
            buffer.AddComponent<Updated>(e);
            buffer.AddComponent<BatchesUpdated>(e);
        }

        private bool Node_SetUpdated(in EntityCommandBuffer buffer, Entity e) {
            TryAddUpdate(buffer, e);

            if (!EntityManager.TryGetBuffer<ConnectedEdge>(e, true, out var cBuffer)) {
                return true;
            }

            for (var i = 0; i < cBuffer.Length; i++) {
                var seg  = cBuffer[i].m_Edge;
                var edge = EntityManager.GetComponentData<Edge>(seg);
                if (!e.Equals(edge.m_Start) && !e.Equals(edge.m_End)) {
                    continue;
                }

                TryAddUpdate(buffer, seg);
                if (!edge.m_Start.Equals(e)) {
                    TryAddUpdate(buffer, edge.m_Start);
                } else if (!edge.m_End.Equals(e)) {
                    TryAddUpdate(buffer, edge.m_End);
                }

                if (!EntityManager.TryGetComponent<Aggregated>(seg, out var aggregated)) {
                    continue;
                }

                TryAddUpdate(buffer, aggregated.m_Aggregate);
            }

            return true;
        }

        /// <summary>
        /// Transitions to STATE 0: No nodes selected.
        /// Sets all nodes in the game as eligible for selection.
        /// </summary>
        private void StateTransitionNoNodes() {
            m_Log.Debug("StateTransitionNoNodes()");

            // Add NT_Eligible to ALL nodes 
            EntityManager.AddComponent<NT_Eligible>(m_NodesWithoutEligibleQuery);
        }

        /// <summary>
        /// Finds all nodes eligible for selection from a starting node.
        /// Traverses in all directions until hitting intersections (>2 edges) or road ends.
        /// The start node itself is always included, even if it's an intersection.
        /// Skips nodes that are already in the current path to avoid backing up.
        /// </summary>
        /// <param name="startNode">The node to start traversal from</param>
        /// <param name="outEligibleNodes">Output list of eligible nodes</param>
        private void FindEligibleNodes(Entity startNode, NativeList<Entity> outEligibleNodes) {
            outEligibleNodes.Clear();

            var toVisit = new NativeQueue<Entity>(Allocator.Temp);
            var visited = new NativeHashSet<Entity>(64, Allocator.Temp);

            // Start node is always eligible
            toVisit.Enqueue(startNode);
            visited.Add(startNode);
            outEligibleNodes.Add(startNode);

            while (toVisit.TryDequeue(out var current)) {
                // Get connected edges
                if (!EntityManager.HasBuffer<ConnectedEdge>(current)) {
                    continue;
                }

                var connectedEdges = EntityManager.GetBuffer<ConnectedEdge>(current);

                // Stop traversing beyond intersections (but not if it's the start node)
                if (connectedEdges.Length > 2 && current != startNode) {
                    continue;
                }

                // Traverse to all neighbors
                for (var i = 0; i < connectedEdges.Length; i++) {
                    var edgeEntity = connectedEdges[i].m_Edge;

                    if (!EntityManager.HasComponent<Edge>(edgeEntity)) {
                        continue;
                    }

                    var edge     = EntityManager.GetComponentData<Edge>(edgeEntity);
                    var neighbor = edge.m_Start == current ? edge.m_End : edge.m_Start;

                    // Skip nodes already in current path (except start node)
                    if (neighbor != startNode && m_CurrentPathNodes.Contains(neighbor)) {
                        continue;
                    }

                    // Only visit if not already visited
                    if (visited.Add(neighbor)) {
                        outEligibleNodes.Add(neighbor);
                        toVisit.Enqueue(neighbor);
                    }
                }
            }

            toVisit.Dispose();
            visited.Dispose();

            m_Log.Debug($"FindEligibleNodes: Found {outEligibleNodes.Length} eligible nodes from start node");
        }

        /// <summary>
        /// Finds the shortest path between two nodes using BFS.
        /// Returns the path including start and end nodes, and the edges connecting them.
        /// </summary>
        /// <param name="startNode">Starting node</param>
        /// <param name="endNode">Ending node</param>
        /// <param name="nodesPath">Output list containing the path from start to end</param>
        /// <param name="edgePath">Output list containing the edges in the path</param>
        /// <returns>True if a path was found, false otherwise</returns>
        private bool FindPathBetween(Entity                 startNode,
                                     Entity                 endNode,
                                     ref NativeList<Entity> nodesPath,
                                     ref NativeList<Entity> edgePath) {
            nodesPath.Clear();
            edgePath.Clear();

            if (startNode == endNode) {
                return true;
            }

            var queue     = new NativeQueue<Entity>(Allocator.Temp);
            var visited   = new NativeHashSet<Entity>(64, Allocator.Temp);
            var parentMap = new NativeHashMap<Entity, Entity>(64, Allocator.Temp);
            var edgeMap   = new NativeHashMap<Entity, Entity>(64, Allocator.Temp);

            queue.Enqueue(startNode);
            visited.Add(startNode);

            var foundPath = false;

            while (queue.TryDequeue(out var currentEntity)) {
                if (currentEntity == endNode) {
                    foundPath = true;
                    break;
                }

                if (!EntityManager.TryGetBuffer<ConnectedEdge>(currentEntity, true, out var connectedEdges)) {
                    continue;
                }

                // Search in both directions
                for (var i = 0; i < connectedEdges.Length; i++) {
                    var edgeEntity = connectedEdges[i].m_Edge;

                    if (!EntityManager.TryGetComponent<Edge>(edgeEntity, out var edge)) {
                        continue;
                    }

                    var neighbor = edge.m_Start == currentEntity ? edge.m_End : edge.m_Start;

                    if (visited.Add(neighbor)) {
                        parentMap[neighbor] = currentEntity;
                        edgeMap[neighbor]   = edgeEntity;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // Reconstruct path from end to start
            if (foundPath) {
                var pathNodes = new NativeList<Entity>(16, Allocator.Temp);
                var pathEdges = new NativeList<Entity>(16, Allocator.Temp);
                var current   = endNode;

                while (current != startNode) {
                    pathNodes.Add(current);
                    if (edgeMap.TryGetValue(current, out var usedEdge)) {
                        pathEdges.Add(usedEdge);
                    }

                    if (!parentMap.TryGetValue(current, out current)) {
                        // Path broken - shouldn't happen
                        foundPath = false;
                        break;
                    }
                }

                if (foundPath) {
                    pathNodes.Add(startNode);

                    // Reverse path to go from start to end
                    for (var i = pathNodes.Length - 1; i >= 0; i--) {
                        nodesPath.Add(pathNodes[i]);
                    }

                    // Reverse edges to go from start to end
                    for (var i = pathEdges.Length - 1; i >= 0; i--) {
                        edgePath.Add(pathEdges[i]);
                    }
                }

                pathNodes.Dispose();
                pathEdges.Dispose();
            }

            queue.Dispose();
            visited.Dispose();
            parentMap.Dispose();
            edgeMap.Dispose();

            m_Log.Debug($"FindPathBetween: Found path with {nodesPath.Length} nodes and {edgePath.Length} edges: {foundPath}");
            return foundPath;
        }

        /// <summary>
        /// Updates highlighting for hovered path.
        /// Highlights both nodes and edges in the path from the last selected node to the hovered node.
        /// </summary>
        private void PreviewPath() {
            // Clear any existing highlights
            ClearAllHighlights();

            // Add highlights to nodes
            var nodesArray = new NativeArray<Entity>(m_NextPathNodes.AsArray(), Allocator.Temp);
            EntityManager.AddComponent<NT_Highlighted>(nodesArray);
            nodesArray.Dispose();

            // Add highlights to edges
            var edgesArray = new NativeArray<Entity>(m_NextPathEdges.AsArray(), Allocator.Temp);
            EntityManager.AddComponent<NT_Highlighted>(edgesArray);
            edgesArray.Dispose();
        }

        /// <summary>
        /// Clears all NT_Highlighted components from nodes and edges (batch operation).
        /// </summary>
        private void ClearAllHighlights() {
            EntityManager.RemoveComponent<NT_Highlighted>(m_NodesWithHighlightedQuery);
            EntityManager.RemoveComponent<NT_Highlighted>(m_EdgesWithHighlightedQuery);
        }

        public void ApplySlopeToSelectedEdges() { ApplySlopeToSelectedEdges(SlopeCurveConfig.Linear()); }

        internal enum ChangeObjectHighlightMode {
            AddHighlight,
            RemoveHighlight,
        }
    }
}