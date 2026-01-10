// <copyright file="NT_NodeSelectionToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using System.Linq;
    using System.Xml;
    using Colossal.Entities;
    using Colossal.Mathematics;
    using Colossal.UI;
    using Game.Common;
    using Game.Input;
    using Game.Net;
    using Game.Notifications;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using NetworkTools.Settings;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using static Colossal.IO.AssetDatabase.AtlasFrame;

    #endregion

    /// <summary>
    /// Selection System that allows selecting a contiguous edge.
    /// Given the game's network edge tree, it selects all edge segments between a start and end node.
    /// This is to allow other tools to manipulate road segments of any length between two nodes.
    /// 
    /// State Machine:
    /// 
    /// NoSelection
    /// - All network nodes in the game have NT_Eligible component
    /// - Actions:
    ///     - [Hover] over NT_Eligible Node: Clear NT_Highlighted. Adds NT_Highlighted to node.
    ///     - [Hover] over nothing: Removes all NT_Highlighted.
    ///     - [Apply]: Transition to `StartNodeSelected` with node.
    ///     - [Cancel]: Exit Tool
    /// 
    /// StartNodeSelected
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
    /// EndNodeSelected
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
    /// 
    /// </summary>
    public partial class NT_ContiguousEdgeSelectionToolSystem : NT_BaseToolSystem {
        /// <summary>
        /// Represents the currentEntity selection state of the tool.
        /// </summary>
        public enum SelectionState {
            NoSelection = 0,
            StartNodeSelected = 1,
            EndNodeSelected = 2,
        }

        private ControlPoint m_ControlPoint;
        private NativeReference<Entity> m_LastHoveredEntity;
        private NativeReference<Entity> m_LastRaycastEntity;
        private float3 m_LastHitPosition;
        private IProxyAction m_ApplyAction;
        private IProxyAction m_SecondaryApplyAction;
        private NativeList<Entity> m_SelectedNodes;
        private NativeList<Entity> m_EligibleNodes;
        
        /// <summary>
        /// Currently selected path of nodes
        /// </summary>
        private NativeList<Entity> m_CurrentPathNodes;

        /// <summary>
        /// Currently selected path of edges
        /// </summary>
        private NativeList<Entity> m_CurrentPathEdges;

        /// <summary>
        /// Next path of nodes (updated on hover)
        /// </summary>
        private NativeList<Entity> m_NextPathNodes;

        /// <summary>
        /// Next path of edges (updated on hover)
        /// </summary>
        private NativeList<Entity> m_NextPathEdges;

        private PrefabBase m_Prefab;
        private const float MaxDistanceToSelect = 16f;
        private EntityQuery m_NodesWithEligibleQuery;
        private EntityQuery m_NodesWithHighlightedQuery;
        private EntityQuery m_NodesWithoutEligibleQuery;
        private EntityQuery m_NodesWithSelectedQuery;
        private EntityQuery m_NodesWithSelectedFirstQuery;
        private EntityQuery m_NodesWithSelectedLastQuery;
        private EntityQuery m_EdgesWithHighlightedQuery;
        private EntityQuery m_EdgesWithSelectedQuery;

        public SelectionState CurrentState => 
             m_SelectedNodes.Length switch {
                 0 => SelectionState.NoSelection,
                 1 => SelectionState.StartNodeSelected,
                 _ => SelectionState.EndNodeSelected
             };

        public override bool TrySetPrefab(PrefabBase prefab) {
            m_Log.Debug($"TrySetPrefab {prefab is NT_ToolPrefab} {m_PrefabSystem.HasComponent<NT_Select>(prefab)}");
            var validRequest = prefab is NT_ToolPrefab && m_PrefabSystem.HasComponent<NT_Select>(prefab);

            if (!validRequest) {
                return false;
            }

            m_Prefab = prefab;
            return true;
        }

        public override PrefabBase GetPrefab() { return m_Prefab; }

        /// <summary>
        /// Gets the array of currently selected node entities.
        /// </summary>
        /// <returns>Array of selected Entity objects.</returns>
        public Entity[] GetSelectedNodes() {
            return m_SelectedNodes.ToArray(Allocator.Temp).ToArray();
        }

        protected override void OnCreate() {
            ShowNodes              = true;
            m_ApplyAction          = NetworkToolsMod.Instance.Settings.GetAction(NetworkToolsModSettings.ApplyActionName);
            m_SecondaryApplyAction = NetworkToolsMod.Instance.Settings.GetAction(NetworkToolsModSettings.SecondaryApplyActionName);
            m_SelectedNodes        = new NativeList<Entity>(4, Allocator.Persistent);
            m_EligibleNodes        = new NativeList<Entity>(64, Allocator.Persistent);
            m_CurrentPathNodes     = new NativeList<Entity>(32, Allocator.Persistent);
            m_CurrentPathEdges     = new NativeList<Entity>(32, Allocator.Persistent);
            m_NextPathNodes        = new NativeList<Entity>(32, Allocator.Persistent);
            m_NextPathEdges        = new NativeList<Entity>(32, Allocator.Persistent);
            m_LastHoveredEntity    = new NativeReference<Entity>(Allocator.Persistent);
            m_LastRaycastEntity    = new NativeReference<Entity>(Allocator.Persistent);

            // Query for nodes without NT_Eligible component
            m_NodesWithoutEligibleQuery = SystemAPI.QueryBuilder()
                                                   .WithAll<Node>()
                                                   .WithNone<Deleted, NT_Eligible>()
                                                   .Build();

            // Query for nodes with NT_Eligible component
            m_NodesWithEligibleQuery = SystemAPI.QueryBuilder()
                                                   .WithAll<Node, NT_Eligible>()
                                                   .WithNone<Deleted>()
                                                   .Build();


            // Query for nodes with NT_Selected component
            m_NodesWithSelectedQuery = SystemAPI.QueryBuilder()
                                                   .WithAll<Node, NT_Selected>()
                                                   .WithNone<Deleted>()
                                                   .Build();

            // Query for nodes with NT_Highlighted component
            m_NodesWithHighlightedQuery = SystemAPI.QueryBuilder()
                                                   .WithAll<Node, NT_Highlighted>()
                                                   .WithNone<Deleted>()
                                                   .Build();

            // Query for nodes with NT_SelectedFirst component
            m_NodesWithSelectedFirstQuery = SystemAPI.QueryBuilder()
                                                     .WithAll<Node, NT_SelectedFirst>()
                                                     .WithNone<Deleted>()
                                                     .Build();

            // Query for nodes with NT_SelectedLast component
            m_NodesWithSelectedLastQuery = SystemAPI.QueryBuilder()
                                                    .WithAll<Node, NT_SelectedLast>()
                                                    .WithNone<Deleted>()
                                                    .Build();

            // Query for edges with NT_Highlighted component
            m_EdgesWithHighlightedQuery = SystemAPI.QueryBuilder()
                                                   .WithAll<Edge, NT_Highlighted>()
                                                   .WithNone<Deleted>()
                                                   .Build();

            // Query for edges with NT_Selected component
            m_EdgesWithSelectedQuery = SystemAPI.QueryBuilder()
                                                .WithAll<Edge, NT_Selected>()
                                                .WithNone<Deleted>()
                                                .Build();

            base.OnCreate();
        }

        protected override void OnDestroy() {
            m_SelectedNodes.Dispose();
            m_EligibleNodes.Dispose();
            m_CurrentPathNodes.Dispose();
            m_CurrentPathEdges.Dispose();
            m_NextPathNodes.Dispose();
            m_NextPathEdges.Dispose();
            m_LastHoveredEntity.Dispose();
            m_LastRaycastEntity.Dispose();

            base.OnDestroy();
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
                var hitPos = controlPoint.m_HitPosition;
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
                m_NextPathNodes.Clear();
                m_NextPathEdges.Clear();
                m_LastHoveredEntity.Value = Entity.Null;
                m_LastHitPosition = float3.zero;
                ClearAllHighlights();
            }

            return inputDeps;
        }

        private void UpdateActions() {
            m_ApplyAction.shouldBeEnabled = true;
            m_SecondaryApplyAction.shouldBeEnabled = true;
        }

        protected override void OnStartRunning() {
            m_LastHitPosition = default;

            StateTransitionNoNodes();

            m_ApplyAction.shouldBeEnabled          = true;
            m_SecondaryApplyAction.shouldBeEnabled = true;
        }

        protected override void OnStopRunning() {
            m_ApplyAction.shouldBeEnabled          = false;
            m_SecondaryApplyAction.shouldBeEnabled = false;
            
            // Clean up all state components
            m_Log.Debug("OnStopRunning: Cleaning up state components");
            
            // Batch remove all marker components using cached queries
            EntityManager.RemoveComponent<NT_Selected>(m_NodesWithSelectedQuery);
            EntityManager.RemoveComponent<NT_Selected>(m_EdgesWithSelectedQuery);
            EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);
            EntityManager.RemoveComponent<NT_Highlighted>(m_NodesWithHighlightedQuery);
            EntityManager.RemoveComponent<NT_Highlighted>(m_EdgesWithHighlightedQuery);
            EntityManager.RemoveComponent<NT_SelectedFirst>(m_NodesWithSelectedFirstQuery);
            EntityManager.RemoveComponent<NT_SelectedLast>(m_NodesWithSelectedLastQuery);
            
            // Clear internal state
            m_SelectedNodes.Clear();
            m_EligibleNodes.Clear();
            m_CurrentPathNodes.Clear();
            m_CurrentPathEdges.Clear();
        }

        private void HandlePathUpdate(ControlPoint controlPoint) { 
            if (CurrentState == SelectionState.NoSelection) return;

            var startNode = m_SelectedNodes[^1];
            var endNode = controlPoint.m_OriginalEntity;

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
                        m_Log.Debug($"[EndNodeSelected -> StartNodeSelected] Removing last end point.");
                    }
                    EntityManager.RemoveComponent<NT_Selected>(lastNode);
                    EntityManager.RemoveComponent<NT_SelectedLast>(lastNode);
                    m_SelectedNodes.RemoveAt(m_SelectedNodes.Length - 1);

                    // Reduce our path - remove nodes and edges until we reach the new last node
                    var newLastNode = m_SelectedNodes[^1];
                    var done = false;
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
            var controlPoint = default(ControlPoint);

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
                    controlPoint = new ControlPoint(edge.m_Start, hit);
                } else if (distanceToEnd < MaxDistanceToSelect && distanceToEnd < distanceToStart) {
                    controlPoint = new ControlPoint(edge.m_End, hit);
                }
            }

            if (EntityManager.HasComponent<NT_Eligible>(entity)) {
                controlPoint = new ControlPoint(entity, hit); 
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
            ChangeHighlighting(oldEntity, ChangeObjectHighlightMode.RemoveHighlight);
            ChangeHighlighting(newEntity, ChangeObjectHighlightMode.AddHighlight);
        }

        private void ChangeHighlighting(Entity entity, ChangeObjectHighlightMode mode) {
            if (entity == Entity.Null || !EntityManager.Exists(entity)) {
                return;
            }

            if (mode == ChangeObjectHighlightMode.AddHighlight) {
                EntityManager.AddComponent<NT_Highlighted>(entity);
                EntityManager.AddComponent<BatchesUpdated>(entity);
            } else if (mode == ChangeObjectHighlightMode.RemoveHighlight) {
                EntityManager.RemoveComponent<NT_Highlighted>(entity);
            }
        }

        private void AddHighlight(Entity entity) {
            EntityManager.AddComponent<NT_Highlighted>(entity);
        }

        private void RemoveHighlight(Entity entity) {
            EntityManager.RemoveComponent<NT_Highlighted>(entity);
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

        public override void GetAvailableSnapMask(out Snap onMask, out Snap offMask) {
            //if (this.m_Prefab != null) {
            //    GetCustomAvailableSnapMask(out onMask, out offMask);
            //    return;
            //}
            base.GetAvailableSnapMask(out onMask, out offMask);
        }

        internal enum ChangeObjectHighlightMode {
            AddHighlight,
            RemoveHighlight,
        }

        public void ApplySlopeToSelectedEdges() {
            var buffer = new EntityCommandBuffer(Allocator.Temp);

            // Validation: At least two nodes selected
            if (m_SelectedNodes.Length < 2) {
                m_Log.Debug("ApplySlopeToSelectedEdges: Not enough nodes selected.");
                return;
            }

            var startNode = m_SelectedNodes[0];
            var endNode = m_SelectedNodes[^1];
            var startNodeData = EntityManager.GetComponentData<Node>(startNode);
            var endNodeData = EntityManager.GetComponentData<Node>(endNode);
            var startHeight = startNodeData.m_Position.y;
            var endHeight = endNodeData.m_Position.y;
            var deltaHeight = endHeight - startHeight;

            // Calculate total length of the path using the edge beziers
            var segmentLengths = new NativeList<float>(m_CurrentPathEdges.Length, Allocator.Temp);
            var totalLength = 0f;

            foreach (var edgeEntity in m_CurrentPathEdges) {
                if (EntityManager.TryGetComponent<Curve>(edgeEntity, out var curve)) {
                    var segmentLength = MathUtils.Length(curve.m_Bezier);
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
            var nodeHeights = new NativeArray<float>(m_CurrentPathNodes.Length, Allocator.Temp);
            var distanceAlongPath = 0f;

            for (var i = 0; i < m_CurrentPathNodes.Length; i++) {
                var ratio = distanceAlongPath / totalLength;
                nodeHeights[i] = startHeight + (deltaHeight * ratio);
                
                m_Log.Debug($"Node {i}: distance={distanceAlongPath:F2}/{totalLength:F2}, ratio={ratio:F3}, height={nodeHeights[i]:F2}");
                
                if (i < segmentLengths.Length) {
                    distanceAlongPath += segmentLengths[i];
                }
            }

            // Update each edge bezier with interpolated control point heights
            for (var i = 0; i < m_CurrentPathEdges.Length; i++) {
                var edgeEntity = m_CurrentPathEdges[i];
                var edge = EntityManager.GetComponentData<Edge>(edgeEntity);
                
                if (!EntityManager.TryGetComponent<Curve>(edgeEntity, out var curve)) {
                    continue;
                }

                var bezier = curve.m_Bezier;
                var heightStart = nodeHeights[i];
                var heightEnd = nodeHeights[i + 1];

                // Calculate parametric positions of control points based on horizontal distance
                var horizontalA = new float3(bezier.a.x, 0f, bezier.a.z);
                var horizontalB = new float3(bezier.b.x, 0f, bezier.b.z);
                var horizontalC = new float3(bezier.c.x, 0f, bezier.c.z);
                var horizontalD = new float3(bezier.d.x, 0f, bezier.d.z);

                var totalHorizontalDist = math.distance(horizontalA, horizontalD);
                
                // Handle edge case where start and end are at same horizontal position
                float bRatio = 1f / 3f;
                float cRatio = 2f / 3f;
                
                if (totalHorizontalDist > 0.01f) {
                    bRatio = math.distance(horizontalA, horizontalB) / totalHorizontalDist;
                    cRatio = math.distance(horizontalA, horizontalC) / totalHorizontalDist;
                }

                // Clamp ratios to [0, 1] range
                bRatio = math.clamp(bRatio, 0f, 1f);
                cRatio = math.clamp(cRatio, 0f, 1f);

                // Set heights with linear interpolation
                bezier.a.y = heightStart;
                bezier.b.y = math.lerp(heightStart, heightEnd, bRatio);
                bezier.c.y = math.lerp(heightStart, heightEnd, cRatio);
                bezier.d.y = heightEnd;

                curve.m_Bezier = bezier;
                buffer.SetComponent(edgeEntity, curve);

                m_Log.Debug($"Edge {i}: heightStart={heightStart:F2}, heightEnd={heightEnd:F2}, bRatio={bRatio:F3}, cRatio={cRatio:F3}");

                // Mark nodes as updated
                Node_SetUpdated(buffer, edge.m_Start);
                Node_SetUpdated(buffer, edge.m_End);
            }

            buffer.Playback(EntityManager);
            buffer.Dispose();
            segmentLengths.Dispose();
            nodeHeights.Dispose();
            
            m_Log.Debug($"ApplySlopeToSelectedEdges: Applied slope from {startHeight:F2} to {endHeight:F2} over {totalLength:F2} units to {m_CurrentPathNodes.Length} nodes and {m_CurrentPathEdges.Length} edges.");
        }

        private void TryAddUpdate(in EntityCommandBuffer buffer, Entity e) {
            buffer.AddComponent<Updated>(e);
            buffer.AddComponent<BatchesUpdated>(e);
        }

        private bool Node_SetUpdated(in EntityCommandBuffer buffer, Entity e) {
            TryAddUpdate(buffer, e);

            if (!EntityManager.TryGetBuffer<ConnectedEdge>(e, true, out var cBuffer))
                return true;

            for (var i = 0; i < cBuffer.Length; i++) {
                var seg = cBuffer[i].m_Edge;
                var edge = EntityManager.GetComponentData<Edge>(seg);
                if (!e.Equals(edge.m_Start) && !e.Equals(edge.m_End))
                    continue;

                TryAddUpdate(buffer, seg);
                if (!edge.m_Start.Equals(e))
                    TryAddUpdate(buffer, edge.m_Start);
                else if (!edge.m_End.Equals(e))
                    TryAddUpdate(buffer, edge.m_End);

                if (!EntityManager.TryGetComponent<Aggregated>(seg, out var aggregated))
                    continue;

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

            while (toVisit.TryDequeue(out Entity current)) {
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

                    var edge = EntityManager.GetComponentData<Edge>(edgeEntity);
                    var neighbor = (edge.m_Start == current) ? edge.m_End : edge.m_Start;

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
        private bool FindPathBetween(
            Entity startNode, 
            Entity endNode, 
            ref NativeList<Entity> nodesPath,
            ref NativeList<Entity> edgePath) {
            nodesPath.Clear();
            edgePath.Clear();

            if (startNode == endNode) {
                return true;
            }
            
            var queue = new NativeQueue<Entity>(Allocator.Temp);
            var visited = new NativeHashSet<Entity>(64, Allocator.Temp);
            var parentMap = new NativeHashMap<Entity, Entity>(64, Allocator.Temp);
            var edgeMap = new NativeHashMap<Entity, Entity>(64, Allocator.Temp);
            
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
                    
                    var neighbor = (edge.m_Start == currentEntity) ? edge.m_End : edge.m_Start;
                    
                    if (visited.Add(neighbor)) {
                        parentMap[neighbor] = currentEntity;
                        edgeMap[neighbor] = edgeEntity;
                        queue.Enqueue(neighbor);
                    }
                }
            }
            
            // Reconstruct path from end to start
            if (foundPath) {
                var pathNodes = new NativeList<Entity>(16, Allocator.Temp);
                var pathEdges = new NativeList<Entity>(16, Allocator.Temp);
                var current = endNode;
                
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

        ///// <summary>
        ///// Transitions to STATE 2: Two nodes selected.
        ///// Marks the last node with NT_Selected and NT_SelectedLast.
        ///// Marks all intermediate nodes with NT_Selected.
        ///// Removes all NT_Eligible and NT_Highlighted components.
        ///// </summary>
        ///// <param name="firstNode">The first selected node</param>
        ///// <param name="lastNode">The last selected node</param>
        //private void TransitionToState2(Entity firstNode, Entity lastNode) {
        //    m_Log.Debug("TransitionToState2()");
            
        //    m_CurrentState = SelectionState.EndNodeSelected;
            
        //    // Add marker to last node
        //    EntityManager.AddComponent<NT_SelectedLast>(lastNode);
            
        //    // Find path between first and last node
        //    var path = new NativeList<Entity>(16, Allocator.Temp);
        //    if (FindPathBetween(firstNode, lastNode, path)) {
        //        // Mark all intermediate nodes as selected (excluding first and last which are already marked)
        //        if (path.Length > 2) {
        //            // Create array of intermediate nodes only
        //            var intermediateNodes = new NativeArray<Entity>(path.Length - 2, Allocator.Temp);
        //            for (int i = 0; i < intermediateNodes.Length; i++) {
        //                intermediateNodes[i] = path[i + 1]; // Skip first node
        //            }
        //            EntityManager.AddComponent<NT_Selected>(intermediateNodes);
        //            intermediateNodes.Dispose();
        //        }
                
        //        m_Log.Debug($"TransitionToState2: Marked {path.Length - 2} intermediate nodes as selected");
        //    }
        //    path.Dispose();
            
        //    // Remove all NT_Eligible and NT_Highlighted components (batch operations)
        //    EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);
        //    EntityManager.RemoveComponent<NT_Highlighted>(m_NodesWithHighlightedQuery);
            
        //    // Clear cached data
        //    m_EligibleNodes.Clear();
        //    m_CurrentPathNodes.Clear();
        //}

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
    }
}