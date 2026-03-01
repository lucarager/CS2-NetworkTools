namespace NetworkTools.Systems.Tools.RoadShape {
    using Colossal.Entities;
    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Tools;
    using NetworkTools.Components;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    public partial class NT_RoadShapeToolSystem {
        /// <summary>
        ///     Updates the operation phase based on the current selection state.
        ///     Should be called after any operation that changes the selected node count.
        /// </summary>
        private void UpdateOperationPhase() {
            // Don't interrupt an active apply operation
            if (Phase == OperationPhase.Applying) {
                return;
            }

            var previousPhase = Phase;

            // Derive phase from node count
            Phase = m_SelectedNodes.Length switch {
                0 => OperationPhase.Idle,
                1 => OperationPhase.Configuring,
                _ => OperationPhase.Ready
            };

            // PHASE TRANSITION: Entering Ready
            if (Phase == OperationPhase.Ready && previousPhase != OperationPhase.Ready)
            {
                CreateTransformHandles();
            }

            // PHASE TRANSITION: Leaving Ready
            else if (Phase != OperationPhase.Ready && previousPhase == OperationPhase.Ready)
            {
                DestroyAllHandles();
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            UpdateActions();

            // Right click => Remove last point from stack
            if (m_SecondaryApplyAction.WasPressedThisFrame()) {
                HandleRemoveNode();
                m_UpdateNeeded = true;
                return inputDeps;
            }

            // Handle was hovered, clicked, or dragged                   
            if (Phase == OperationPhase.Ready && ProcessHandleInput()) { 
                return HandleTempEntities(inputDeps); 
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
                    m_UpdateNeeded = true;
                }

                // Update Cache
                m_LastHoveredEntity.Value = controlPoint.m_OriginalEntity;
                m_LastHitPosition         = hitPos;

                // Handle clicking
                if (m_ApplyAction.WasPressedThisFrame()) {
                    HandleAddNode(controlPoint.m_OriginalEntity);
                    m_UpdateNeeded = true;
                }
            }
            else {
                // No entity under cursor
                HandleNoHover();
            }

            // Handle temp entities
            return HandleTempEntities(inputDeps);
        }

        /// <summary>
        ///     Runs various jobs depending on whether we need to Update, Apply, or Cancel temp entities
        /// </summary>
        /// <param name="inputDeps"></param>
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
            m_NextPathNodes.Clear();
            m_NextPathEdges.Clear();
            m_LastHoveredEntity.Value = Entity.Null;
            m_LastHitPosition         = float3.zero;
            ClearAllHighlights();
        }

        private void HandlePathUpdate(ControlPoint controlPoint) {
            if (CurrentSelectionState == SelectionState.NoSelection) {
                return;
            }

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
            switch (CurrentSelectionState) {
                case SelectionState.NoSelection:
                    m_Log.Debug("[NoSelection] Hovering over potential start point.");
                    SwapHighlitedEntities(m_LastHoveredEntity.Value, controlPoint.m_OriginalEntity, NT_Highlighted.DefaultNode);
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
            if (Phase != OperationPhase.Ready) {
                return;
            }

            Phase = OperationPhase.Applying;
        }

        private void HandleAddNode(Entity entity) {
            if (entity == Entity.Null || m_SelectedNodes.Contains(entity)) {
                return;
            }

            // Add Node
            switch (CurrentSelectionState) {
                case SelectionState.NoSelection:
                    m_Log.Debug("[NoSelection -> StartNodeSelected] Adding start point.");
                    m_SelectedNodes.Add(entity);

                    // Add markers to first node
                    EntityManager.AddComponentData(entity, NT_Selected.DefaultNode);
                    EntityManager.AddComponent<NT_SelectedFirst>(entity);
                    break;
                case SelectionState.StartNodeSelected:
                    m_Log.Debug("[StartNodeSelected -> EndNodeSelected] Adding end point.");
                    m_SelectedNodes.Add(entity);

                    // Add markers to end node
                    EntityManager.AddComponentData(entity, NT_Selected.DefaultNode);
                    EntityManager.AddComponent<NT_SelectedLast>(entity);

                    break;
                case SelectionState.EndNodeSelected:
                    m_Log.Debug("[EndNodeSelected] Adding another end point.");

                    // Remove marker from previous end node
                    var lastNode = m_SelectedNodes[^1];
                    EntityManager.RemoveComponent<NT_SelectedLast>(lastNode);

                    m_SelectedNodes.Add(entity);

                    // Add markers to new end node
                    EntityManager.AddComponentData(entity, NT_Selected.DefaultNode);
                    EntityManager.AddComponent<NT_SelectedLast>(entity);

                    break;
            }

            // Add the nodes to our cache and mark as selected
            foreach (var node in m_NextPathNodes)
                if (!m_CurrentPathNodes.Contains(node)) {
                    m_CurrentPathNodes.Add(node);
                    EntityManager.AddComponentData(node, NT_Selected.DefaultNode);
                }

            // Add the edges to our cache and mark as selected
            foreach (var edge in m_NextPathEdges)
                if (!m_CurrentPathEdges.Contains(edge)) {
                    m_CurrentPathEdges.Add(edge);
                    EntityManager.AddComponentData(edge, NT_Selected.DefaultEdge);
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

            // Update phase based on new node count
            UpdateOperationPhase();
        }

        private void HandleRemoveNode() {
            var lastNode = m_SelectedNodes[^1];
            switch (CurrentSelectionState) {
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
                        m_Log.Debug(
                            $"[EndNodeSelected] Removing an end point. {m_SelectedNodes.Length - 1} end points remaining");
                    }
                    else {
                        m_Log.Debug("[EndNodeSelected -> StartNodeSelected] Removing last end point.");
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

                    // Update all path indices to ensure they're consecutive and correct
                    UpdatePathIndices();

                    // Remove NT_Eligible from ALL nodes (we will recalculate based on state)
                    EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);

                    // Find all eligible nodes from new head of path
                    FindEligibleNodes(newLastNode, m_EligibleNodes);

                    // Add NT_Eligible to eligible nodes
                    var eligibleArray = new NativeArray<Entity>(m_EligibleNodes.AsArray(), Allocator.Temp);
                    EntityManager.AddComponent<NT_Eligible>(eligibleArray);
                    eligibleArray.Dispose();

                    if (m_SelectedNodes.Length >= 2) {
                        // Mark the new last node if we still have at least 2
                        EntityManager.AddComponent<NT_SelectedLast>(newLastNode);
                    }

                    // Update phase based on new node count
                    break;
            }

            // Update phase based on new node count
            UpdateOperationPhase();
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

        /// <summary>
        ///     Updates the PathIndex for all nodes in m_CurrentPathNodes to reflect their position in the path.
        ///     This should be called after any add/remove operation to keep indices synchronized.
        /// </summary>
        private void UpdatePathIndices() {
            for (var i = 0; i < m_CurrentPathNodes.Length; i++) {
                var node = m_CurrentPathNodes[i];
                if (EntityManager.HasComponent<NT_Selected>(node)) {
                    EntityManager.SetComponentData(node, NT_Selected.ForNode(NodeRenderMode.RenderAsCircle, i));
                }
            }
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

        public void ResetToIdle() {
            // Clear state to completely blank
            Phase = OperationPhase.Idle;

            // Batch remove all marker components using cached queries
            EntityManager.RemoveComponent<NT_Selected>(m_NodesWithSelectedQuery);
            EntityManager.RemoveComponent<NT_Selected>(m_EdgesWithSelectedQuery);
            EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);
            EntityManager.RemoveComponent<NT_Highlighted>(m_NodesWithHighlightedQuery);
            EntityManager.RemoveComponent<NT_Highlighted>(m_EdgesWithHighlightedQuery);
            EntityManager
                .RemoveComponent<NT_SelectedFirst>(m_NodesWithSelectedFirstQuery);
            EntityManager
                .RemoveComponent<NT_SelectedLast>(m_NodesWithSelectedLastQuery);

            // Reset state
            StateTransitionNoNodes();
        }

        /// <summary>
        ///     Transitions to STATE 0: No nodes selected.
        ///     Sets all nodes in the game as eligible for selection.
        /// </summary>
        private void StateTransitionNoNodes() {
            m_Log.Debug("StateTransitionNoNodes()");

            // Clear caches
            m_SelectedNodes.Clear();
            m_EligibleNodes.Clear();
            m_CurrentPathNodes.Clear();
            m_CurrentPathEdges.Clear();

            // Add NT_Eligible to ALL nodes 
            EntityManager.AddComponent<NT_Eligible>(m_NodesWithoutEligibleQuery);
        }

        /// <summary>
        ///     Finds all nodes eligible for selection from a starting node.
        ///     Traverses in all directions until hitting intersections (>2 edges) or road ends.
        ///     The start node itself is always included, even if it's an intersection.
        ///     Skips nodes that are already in the current path to avoid backing up.
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

                    var edge = EntityManager.GetComponentData<Edge>(edgeEntity);
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
        ///     Finds the shortest path between two nodes using BFS.
        ///     Returns the path including start and end nodes, and the edges connecting them.
        /// </summary>
        /// <param name="startNode">Starting node</param>
        /// <param name="endNode">Ending node</param>
        /// <param name="nodesPath">Output list containing the path from start to end</param>
        /// <param name="edgePath">Output list containing the edges in the path</param>
        /// <returns>True if a path was found, false otherwise</returns>
        private bool FindPathBetween(Entity startNode,
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
            var edgeMap = new NativeHashMap<Entity, Entity>(64,   Allocator.Temp);

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
                    for (var i = pathNodes.Length - 1; i >= 0; i--)
                        nodesPath.Add(pathNodes[i]);

                    // Reverse edges to go from start to end
                    for (var i = pathEdges.Length - 1; i >= 0; i--)
                        edgePath.Add(pathEdges[i]);
                }

                pathNodes.Dispose();
                pathEdges.Dispose();
            }

            queue.Dispose();
            visited.Dispose();
            parentMap.Dispose();
            edgeMap.Dispose();

            m_Log.Debug(
                $"FindPathBetween: Found path with {nodesPath.Length} nodes and {edgePath.Length} edges: {foundPath}");
            return foundPath;
        }

        /// <summary>
        ///     Updates highlighting for hovered path.
        ///     Highlights both nodes and edges in the path from the last selected node to the hovered node.
        /// </summary>
        private void PreviewPath() {
            // Clear any existing highlights
            ClearAllHighlights();

            // Add highlights to nodes
            foreach (var node in m_NextPathNodes)
                AddHighlight(node, NT_Highlighted.DefaultNode);

            // Add highlights to edges
            foreach (var edge in m_NextPathEdges) {
                AddHighlight(edge, NT_Highlighted.DefaultEdge);
            }
        }
    }
}