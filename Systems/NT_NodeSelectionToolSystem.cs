// <copyright file="NT_NodeSelectionToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    #region Using Statements

    using System.Linq;
    using Colossal.Mathematics;
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

    #endregion

    #endregion

    /// <summary>
    /// Node selection tool system that manages selection of two nodes connected by a road segment.
    /// This is to allow other tools to manipulate road segments of any length between two nodes (containing between them 0 to N nodes).
    /// 
    /// Any two nodes that are connected by uninterrupted road segments can be selected.
    /// Any node betwen a Start Node and the last possible End node of a segment is also selectable.
    /// 
    /// State Machine:
    /// 
    /// STATE 0: No nodes selected
    /// - All nodes in the game have NT_Eligible component
    /// - User can click any node to select it
    /// 
    /// STATE 1: One node selected
    /// - First node has: NT_Selected, NT_SelectedFirst
    /// - Any eligible nodes (reachable via uninterrupted road segments) have: NT_Eligible
    /// - Non-eligible nodes have no NT_Eligible
    /// - Hovering over eligible node: path nodes get NT_Highlighted
    /// - User can click eligible node to select second, or right-click to deselect first
    /// 
    /// STATE 2: Two nodes selected
    /// - First node has: NT_Selected, NT_SelectedFirst
    /// - Last node has: NT_Selected, NT_SelectedLast
    /// - All intermediate path nodes have: NT_Selected
    /// - No nodes have: NT_Eligible or NT_Highlighted
    /// - User can right-click to remove last node (returns to STATE 1)
    /// 
    /// Transitions:
    /// - STATE 0 -> STATE 1: Click any node
    /// - STATE 1 -> STATE 0: Right-click to remove first node
    /// - STATE 1 -> STATE 2: Click eligible node
    /// - STATE 2 -> STATE 1: Right-click to remove second node
    /// </summary>
    public partial class NT_NodeSelectionToolSystem : NT_BaseToolSystem {
        /// <summary>
        /// Represents the current selection state of the tool.
        /// </summary>
        private enum SelectionState {
            /// <summary>STATE 0: No nodes selected, all nodes eligible</summary>
            NoSelection = 0,
            
            /// <summary>STATE 1: First node selected, only reachable nodes eligible</summary>
            FirstNodeSelected = 1,
            
            /// <summary>STATE 2: Two nodes selected, path established</summary>
            BothNodesSelected = 2
        }
        
        private       ControlPoint            m_ControlPoint;
        private       NativeReference<Entity> m_LastHoveredEntity;
        private       NativeReference<Entity> m_LastRaycastEntity;
        private       float3                  m_LastHitPosition;
        private       IProxyAction            m_ApplyAction;
        private       IProxyAction            m_SecondaryApplyAction;
        private       NativeList<Entity>      m_SelectedNodes;
        private       NativeList<Entity>      m_EligibleNodes;      // STATE 1: Cached eligible nodes
        private       NativeList<Entity>      m_CurrentPath;        // STATE 1: Current hover path for highlighting
        private       SelectionState          m_CurrentState;       // Current state of the selection
        private       PrefabBase              m_Prefab;
        private const uint                    MaxNodes            = 2;
        private const float                   MaxDistanceToSelect = 16f;
        private       EntityQuery             m_NodesWithEligibleQuery;
        private       EntityQuery             m_NodesWithHighlightedQuery;
        private       EntityQuery             m_NodesWithoutEligibleQuery;
        private       EntityQuery             m_NodesWithSelectedQuery;
        private       EntityQuery             m_NodesWithSelectedFirstQuery;
        private       EntityQuery             m_NodesWithSelectedLastQuery;

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
            m_CurrentPath          = new NativeList<Entity>(16, Allocator.Persistent);
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

            base.OnCreate();
        }

        protected override void OnDestroy() {
            m_SelectedNodes.Dispose();
            m_EligibleNodes.Dispose();
            m_CurrentPath.Dispose();
            m_LastHoveredEntity.Dispose();
            m_LastRaycastEntity.Dispose();

            base.OnDestroy();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            const string logPrefix = "OnUpdate()";
            UpdateActions();

            // Right click => Remove last point from stack
            if (m_SecondaryApplyAction.WasPressedThisFrame()) {
                RemoveLastPoint();
                return inputDeps;
            }

            // Get raycast result
            if (GetRaycastResult(out var controlPoint)) {
                var hitPos = controlPoint.m_HitPosition;

                if (m_LastHoveredEntity.Value != controlPoint.m_OriginalEntity) {
                    // Handle hover based on current state
                    if (m_CurrentState == SelectionState.FirstNodeSelected) {
                        // In STATE 1: highlight entire path to hovered node
                        UpdateHoverHighlighting(controlPoint.m_OriginalEntity);
                    } else {
                        // In other states: simple single-node highlighting
                        SwapHighlitedEntities(m_LastHoveredEntity.Value, controlPoint.m_OriginalEntity);
                    }
                }

                m_LastHoveredEntity.Value = controlPoint.m_OriginalEntity;
                m_LastHitPosition         = hitPos;

                if (m_ApplyAction.WasPressedThisFrame()) {
                    AddPoint(controlPoint.m_OriginalEntity);
                }
            } else {
                // No entity under cursor - clear highlighting
                if (m_CurrentState == SelectionState.FirstNodeSelected) {
                    ClearAllHighlights();
                    m_CurrentPath.Clear();
                } else {
                    ChangeHighlighting(m_LastHoveredEntity.Value, ChangeObjectHighlightMode.RemoveHighlight);
                }
                m_LastHoveredEntity.Value = Entity.Null;
                m_LastHitPosition         = float3.zero;
            }

            return inputDeps;
        }

        private void UpdateActions() {
            m_ApplyAction.shouldBeEnabled = true;
            m_SecondaryApplyAction.shouldBeEnabled = true;
        }

        protected override void OnStartRunning() {
            m_LastHitPosition = default;

            m_ApplyAction.shouldBeEnabled          = true;
            m_SecondaryApplyAction.shouldBeEnabled = true;
            
            // Initialize to STATE 0
            TransitionToState0();
            m_Log.Debug("OnStartRunning: Initialized to STATE 0");
        }

        protected override void OnStopRunning() {
            m_ApplyAction.shouldBeEnabled          = false;
            m_SecondaryApplyAction.shouldBeEnabled = false;
            
            // Clean up all state components
            m_Log.Debug("OnStopRunning: Cleaning up state components");
            
            // Batch remove all marker components using cached queries
            EntityManager.RemoveComponent<NT_Selected>(m_NodesWithSelectedQuery);
            EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);
            EntityManager.RemoveComponent<NT_Highlighted>(m_NodesWithHighlightedQuery);
            EntityManager.RemoveComponent<NT_SelectedFirst>(m_NodesWithSelectedFirstQuery);
            EntityManager.RemoveComponent<NT_SelectedLast>(m_NodesWithSelectedLastQuery);
            
            // Clear internal state
            m_SelectedNodes.Clear();
            m_EligibleNodes.Clear();
            m_CurrentPath.Clear();
            m_CurrentState = SelectionState.NoSelection;
        }

        private void AddPoint(Entity entity) {
            if (entity == Entity.Null || m_SelectedNodes.Contains(entity)) {
                return;
            }

            // STATE 0 -> STATE 1: First node selection
            if (m_SelectedNodes.Length == 0) {
                m_SelectedNodes.Add(entity);
                TransitionToState1(entity);
                m_Log.Debug($"AddPoint: Transitioned to STATE 1 with first node");
            }
            // STATE 1 -> STATE 2: Second node selection
            else if (m_SelectedNodes.Length == 1) {
                // Validate that the second node is eligible
                if (!EntityManager.HasComponent<NT_Eligible>(entity)) {
                    m_Log.Warn($"AddPoint: Attempted to select ineligible node as second node");
                    return;
                }
                
                m_SelectedNodes.Add(entity);
                EntityManager.AddComponent<NT_Selected>(entity);
                TransitionToState2(m_SelectedNodes[0], entity);
                m_Log.Debug($"AddPoint: Transitioned to STATE 2 with second node");
            }
            // Already have max nodes
            else if (m_SelectedNodes.Length >= MaxNodes) {
                m_Log.Debug($"AddPoint: Already at max nodes ({MaxNodes})");
                return;
            }
        }

        private void RemoveLastPoint() {
            if (m_SelectedNodes.Length == 0) {
                return;
            }

            var lastNode = m_SelectedNodes[^1];
            
            // STATE 1 -> STATE 0: Remove first (and only) node
            if (m_SelectedNodes.Length == 1) {
                m_Log.Debug("RemoveLastPoint: STATE 1 -> STATE 0");
                
                // Remove components from first node
                EntityManager.RemoveComponent<NT_Selected>(lastNode);
                EntityManager.RemoveComponent<NT_SelectedFirst>(lastNode);
                
                // Remove from list
                m_SelectedNodes.RemoveAt(0);
                
                // Transition to STATE 0
                TransitionToState0();
            }
            // STATE 2 -> STATE 1: Remove second node
            else if (m_SelectedNodes.Length == 2) {
                m_Log.Debug("RemoveLastPoint: STATE 2 -> STATE 1");
                
                var firstNode = m_SelectedNodes[0];
                
                // Remove components from last node
                EntityManager.RemoveComponent<NT_Selected>(lastNode);
                EntityManager.RemoveComponent<NT_SelectedLast>(lastNode);
                
                // Remove NT_Selected from all intermediate nodes (batch operation)
                var allSelectedNodes = m_NodesWithSelectedQuery.ToEntityArray(Allocator.Temp);
                foreach (var node in allSelectedNodes) {
                    // Keep first node selected, remove from all others
                    if (node != firstNode) {
                        EntityManager.RemoveComponent<NT_Selected>(node);
                    }
                }
                allSelectedNodes.Dispose();
                
                // Remove from list
                m_SelectedNodes.RemoveAt(1);
                
                // Transition back to STATE 1
                TransitionToState1(firstNode);
            }
        }

        private void RemovePoint(Entity entity) {
            if (m_SelectedNodes.Length == 0) {
                return;
            }

            EntityManager.RemoveComponent<NT_Selected>(entity);
            var index = m_SelectedNodes.IndexOf(entity);
            if (index == -1) {
                return;
            }
            m_SelectedNodes.RemoveAt(index);
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
            if (EntityManager.HasComponent<Edge>(entity)) {
                // todo make job
                // Find the closest node to the hit position
                var edge            = EntityManager.GetComponentData<Edge>(entity);
                var startNode       = EntityManager.GetComponentData<Node>(edge.m_Start);
                var distanceToStart = math.distance(hit.m_Position, startNode.m_Position);
                var endNode         = EntityManager.GetComponentData<Node>(edge.m_End);
                var distanceToEnd   = math.distance(hit.m_Position, endNode.m_Position);

                if (distanceToStart < MaxDistanceToSelect && distanceToStart < distanceToEnd) {
                    return new ControlPoint(edge.m_Start, hit);
                } 
                if (distanceToEnd < MaxDistanceToSelect && distanceToEnd < distanceToStart) {
                    return new ControlPoint(edge.m_End, hit);
                }

                return default;
            }

            return new ControlPoint(entity, hit);
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

        //private static void GetCustomAvailableSnapMask(out Snap onMask, out Snap offMask) {
        //    onMask = Snap.ExistingGeometry | Snap.CellLength | Snap.StraightDirection | Snap.ObjectSide | Snap.GuideLines | Snap.ZoneGrid | Snap.ContourLines
        // | Snap.ObjectSurface | Snap.LotGrid | Snap.AutoParent;
        //    offMask = onMask;
        //}

        internal enum ChangeObjectHighlightMode {
            AddHighlight,
            RemoveHighlight,
        }

        /// <summary>
        /// Transitions to STATE 0: No nodes selected.
        /// Sets all nodes in the game as eligible for selection.
        /// </summary>
        private void TransitionToState0() {
            m_Log.Debug("TransitionToState0()");
            
            m_CurrentState = SelectionState.NoSelection;
            
            // Clear cached data
            m_EligibleNodes.Clear();
            m_CurrentPath.Clear();
            
            // Add NT_Eligible to ALL nodes (batch operation)
            EntityManager.AddComponent<NT_Eligible>(m_NodesWithoutEligibleQuery);
        }

        /// <summary>
        /// Finds all nodes eligible for selection from a starting node.
        /// Traverses in all directions until hitting intersections (>2 edges) or road ends.
        /// The start node itself is always included, even if it's an intersection.
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
                for (int i = 0; i < connectedEdges.Length; i++) {
                    var edgeEntity = connectedEdges[i].m_Edge;
                    
                    if (!EntityManager.HasComponent<Edge>(edgeEntity)) {
                        continue;
                    }
                    
                    var edge = EntityManager.GetComponentData<Edge>(edgeEntity);
                    var neighbor = (edge.m_Start == current) ? edge.m_End : edge.m_Start;
                    
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
        /// Transitions to STATE 1: One node selected.
        /// Marks the first node with NT_Selected and NT_SelectedFirst.
        /// Finds and marks eligible nodes reachable via uninterrupted road segments.
        /// </summary>
        /// <param name="firstNode">The first selected node</param>
        private void TransitionToState1(Entity firstNode) {
            m_Log.Debug("TransitionToState1()");
            
            m_CurrentState = SelectionState.FirstNodeSelected;
            
            // Add markers to first node
            EntityManager.AddComponent<NT_Selected>(firstNode);
            EntityManager.AddComponent<NT_SelectedFirst>(firstNode);
            
            // Find all eligible nodes from first node
            FindEligibleNodes(firstNode, m_EligibleNodes);
            
            // Remove NT_Eligible from ALL nodes (batch operation)
            EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);
            
            // Add NT_Eligible to eligible nodes
            var eligibleArray = new NativeArray<Entity>(m_EligibleNodes.AsArray(), Allocator.Temp);
            EntityManager.AddComponent<NT_Eligible>(eligibleArray);
            eligibleArray.Dispose();
            
            m_Log.Debug($"TransitionToState1: Marked {m_EligibleNodes.Length} nodes as eligible");
        }

        /// <summary>
        /// Finds the shortest path between two nodes using BFS.
        /// Returns the path including start and end nodes.
        /// </summary>
        /// <param name="startNode">Starting node</param>
        /// <param name="endNode">Ending node</param>
        /// <param name="outPath">Output list containing the path from start to end</param>
        /// <returns>True if a path was found, false otherwise</returns>
        private bool FindPathBetween(Entity startNode, Entity endNode, NativeList<Entity> outPath) {
            outPath.Clear();
            
            if (startNode == endNode) {
                outPath.Add(startNode);
                return true;
            }
            
            var queue = new NativeQueue<Entity>(Allocator.Temp);
            var visited = new NativeHashSet<Entity>(64, Allocator.Temp);
            var parentMap = new NativeHashMap<Entity, Entity>(64, Allocator.Temp);
            
            queue.Enqueue(startNode);
            visited.Add(startNode);
            
            bool foundPath = false;
            
            while (queue.TryDequeue(out Entity current)) {
                if (current == endNode) {
                    foundPath = true;
                    break;
                }
                
                if (!EntityManager.HasBuffer<ConnectedEdge>(current)) {
                    continue;
                }
                
                var connectedEdges = EntityManager.GetBuffer<ConnectedEdge>(current);
                
                for (int i = 0; i < connectedEdges.Length; i++) {
                    var edgeEntity = connectedEdges[i].m_Edge;
                    
                    if (!EntityManager.HasComponent<Edge>(edgeEntity)) {
                        continue;
                    }
                    
                    var edge = EntityManager.GetComponentData<Edge>(edgeEntity);
                    var neighbor = (edge.m_Start == current) ? edge.m_End : edge.m_Start;
                    
                    if (visited.Add(neighbor)) {
                        parentMap[neighbor] = current;
                        queue.Enqueue(neighbor);
                    }
                }
            }
            
            // Reconstruct path from end to start
            if (foundPath) {
                var path = new NativeList<Entity>(16, Allocator.Temp);
                var current = endNode;
                
                while (current != startNode) {
                    path.Add(current);
                    if (!parentMap.TryGetValue(current, out current)) {
                        // Path broken - shouldn't happen
                        foundPath = false;
                        break;
                    }
                }
                
                if (foundPath) {
                    path.Add(startNode);
                    
                    // Reverse path to go from start to end
                    for (int i = path.Length - 1; i >= 0; i--) {
                        outPath.Add(path[i]);
                    }
                }
                
                path.Dispose();
            }
            
            queue.Dispose();
            visited.Dispose();
            parentMap.Dispose();
            
            m_Log.Debug($"FindPathBetween: Found path with {outPath.Length} nodes: {foundPath}");
            return foundPath;
        }

        /// <summary>
        /// Transitions to STATE 2: Two nodes selected.
        /// Marks the last node with NT_Selected and NT_SelectedLast.
        /// Marks all intermediate nodes with NT_Selected.
        /// Removes all NT_Eligible and NT_Highlighted components.
        /// </summary>
        /// <param name="firstNode">The first selected node</param>
        /// <param name="lastNode">The last selected node</param>
        private void TransitionToState2(Entity firstNode, Entity lastNode) {
            m_Log.Debug("TransitionToState2()");
            
            m_CurrentState = SelectionState.BothNodesSelected;
            
            // Add marker to last node
            EntityManager.AddComponent<NT_SelectedLast>(lastNode);
            
            // Find path between first and last node
            var path = new NativeList<Entity>(16, Allocator.Temp);
            if (FindPathBetween(firstNode, lastNode, path)) {
                // Mark all intermediate nodes as selected (excluding first and last which are already marked)
                if (path.Length > 2) {
                    // Create array of intermediate nodes only
                    var intermediateNodes = new NativeArray<Entity>(path.Length - 2, Allocator.Temp);
                    for (int i = 0; i < intermediateNodes.Length; i++) {
                        intermediateNodes[i] = path[i + 1]; // Skip first node
                    }
                    EntityManager.AddComponent<NT_Selected>(intermediateNodes);
                    intermediateNodes.Dispose();
                }
                
                m_Log.Debug($"TransitionToState2: Marked {path.Length - 2} intermediate nodes as selected");
            }
            path.Dispose();
            
            // Remove all NT_Eligible and NT_Highlighted components (batch operations)
            EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);
            EntityManager.RemoveComponent<NT_Highlighted>(m_NodesWithHighlightedQuery);
            
            // Clear cached data
            m_EligibleNodes.Clear();
            m_CurrentPath.Clear();
        }

        /// <summary>
        /// Updates highlighting for hovered node in STATE 1.
        /// Highlights the path from first selected node to the hovered node.
        /// Only works if hovered node is eligible.
        /// </summary>
        /// <param name="hoveredNode">The node being hovered over</param>
        private void UpdateHoverHighlighting(Entity hoveredNode) {
            // Only apply in STATE 1 (one node selected)
            if (m_CurrentState != SelectionState.FirstNodeSelected) {
                return;
            }
            
            // Only highlight if hovering over eligible node
            if (hoveredNode == Entity.Null || !EntityManager.HasComponent<NT_Eligible>(hoveredNode)) {
                // Clear any existing highlights
                ClearAllHighlights();
                m_CurrentPath.Clear();
                return;
            }
            
            var firstNode = m_SelectedNodes[0];
            
            // Find path from first node to hovered node
            var newPath = new NativeList<Entity>(16, Allocator.Temp);
            if (FindPathBetween(firstNode, hoveredNode, newPath)) {
                // Check if path changed
                bool pathChanged = newPath.Length != m_CurrentPath.Length;
                if (!pathChanged) {
                    for (int i = 0; i < newPath.Length; i++) {
                        if (newPath[i] != m_CurrentPath[i]) {
                            pathChanged = true;
                            break;
                        }
                    }
                }
                
                // Only update if path changed
                if (pathChanged) {
                    // Clear old highlights
                    ClearAllHighlights();
                    
                    // Store new path
                    m_CurrentPath.Clear();
                    m_CurrentPath.AddRange(newPath);
                    
                    // Add highlights to new path (batch operation)
                    var pathArray = new NativeArray<Entity>(m_CurrentPath.AsArray(), Allocator.Temp);
                    EntityManager.AddComponent<NT_Highlighted>(pathArray);
                    EntityManager.AddComponent<BatchesUpdated>(pathArray);
                    pathArray.Dispose();
                }
            }
            
            newPath.Dispose();
        }

        /// <summary>
        /// Clears all NT_Highlighted components from nodes (batch operation).
        /// </summary>
        private void ClearAllHighlights() {
            EntityManager.RemoveComponent<NT_Highlighted>(m_NodesWithHighlightedQuery);
        }
    }
}