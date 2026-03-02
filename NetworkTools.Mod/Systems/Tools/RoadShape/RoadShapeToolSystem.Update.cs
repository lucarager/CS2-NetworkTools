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
        /// Updates the operation phase based on the current selection state.
        /// Called after selection changes. Returns the previous phase for transition detection.
        /// </summary>
        private OperationPhase UpdateOperationPhase() {
            // Don't interrupt an active apply operation
            if (Phase == OperationPhase.Applying) {
                return Phase;
            }

            var previousPhase = Phase;

            // Derive phase from node count
            Phase = m_SelectedNodes.Length switch {
                0 => OperationPhase.Idle,
                1 => OperationPhase.Configuring,
                _ => OperationPhase.Ready
            };

            return previousPhase;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            UpdateActions();

            // ═══════════════════════════════════════════════════════════════════════════
            // HANDLE INTERACTION PIPELINE 
            // ═══════════════════════════════════════════════════════════════════════════

            if (Phase == OperationPhase.Ready && ProcessHandleInput()) {
                // Handle consumed input this frame:
                // - OnHandleDragging() may have updated ShapeTransformConfig
                // - m_UpdateNeeded was set to true
                // - Skip node selection, go straight to output
                return HandleTempEntities(inputDeps);
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // NODE SELECTION: Input Detection 
            // ═══════════════════════════════════════════════════════════════════════════

            var rightClickPressed = m_SecondaryApplyAction.WasPressedThisFrame();
            var leftClickPressed = m_ApplyAction.WasPressedThisFrame();
            var raycastHit = false;
            var hoveredEntity = Entity.Null;
            var hitPosition = float3.zero;
            ControlPoint controlPoint = default;

            raycastHit = GetRaycastResult(out controlPoint);
            if (raycastHit) {
                hoveredEntity = controlPoint.m_OriginalEntity;
                hitPosition = controlPoint.m_HitPosition;
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // NODE SELECTION: State Mutation
            // ═══════════════════════════════════════════════════════════════════════════

            var selectionChanged = false;

            // Right-click: cancel/back (skips all raycast processing)
            if (rightClickPressed) {
                selectionChanged = HandleRemoveNode();
                m_UpdateNeeded = true;
            }
            // Raycast-based interactions
            else if (raycastHit) {
                // Update hover state first (so path preview is ready if user clicks)
                var newEntityHovered = (hoveredEntity != m_LastHoveredEntity.Value);
                if (newEntityHovered) {
                    HandlePathUpdate(controlPoint);
                    HandleHover(hoveredEntity);
                    m_UpdateNeeded = true;
                }
                m_LastHoveredEntity.Value = hoveredEntity;
                m_LastHitPosition = hitPosition;

                // Left-click: add node (after hover update, same frame OK)
                if (leftClickPressed && hoveredEntity != Entity.Null) {
                    selectionChanged = HandleAddNode(hoveredEntity);
                    m_UpdateNeeded = true;
                }
            }
            // No raycast hit
            else {
                HandleNoHover();
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // PHASE TRANSITION & PATH DATA REFRESH
            // ═══════════════════════════════════════════════════════════════════════════

            if (selectionChanged) {
                var previousPhase = UpdateOperationPhase();

                // In Ready: refresh path data + handles (covers entering AND extending)
                if (Phase == OperationPhase.Ready) {
                    RefreshPathData();
                    RefreshTransformHandles();
                }
                // Exiting Ready: clean up handles
                else if (previousPhase == OperationPhase.Ready) {
                    DestroyAllHandles();
                }
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // OUTPUT
            // ═══════════════════════════════════════════════════════════════════════════

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

        /// <summary>
        /// Updates highlighting based on new hover state.
        /// </summary>
        private void HandleHover(Entity hoveredEntity) {
            switch (CurrentSelectionState) {
                case SelectionState.NoSelection:
                    m_Log.Debug("[NoSelection] Hovering over potential start point.");
                    SwapHighlitedEntities(m_LastHoveredEntity.Value, hoveredEntity, NT_Highlighted.DefaultNode);
                    break;
                case SelectionState.StartNodeSelected:
                    PreviewPath();
                    m_Log.Debug("[StartNodeSelected] Hovering over potential end point.");
                    break;
                case SelectionState.EndNodeSelected:
                    PreviewPath();
                    m_Log.Debug("[EndNodeSelected] Hovering over another potential end point.");
                    break;
            }
        }

        public void RequestApply() {
            if (Phase != OperationPhase.Ready) {
                return;
            }

            Phase = OperationPhase.Applying;
        }

        /// <summary>
        /// Attempts to add a node to the path. Returns true if selection changed.
        /// Updates ECS markers, path caches, and eligible nodes.
        /// </summary>
        private bool HandleAddNode(Entity entity) {
            if (entity == Entity.Null || m_SelectedNodes.Contains(entity)) {
                return false;
            }

            m_Log.Debug($"[{CurrentSelectionState}] Adding node: {entity}");

            // 1. Add node to selection and mark with state-specific components
            switch (CurrentSelectionState) {
                case SelectionState.NoSelection:
                    m_Log.Debug("→ StartNodeSelected");
                    m_SelectedNodes.Add(entity);
                    EntityManager.AddComponentData(entity, NT_Selected.DefaultNode);
                    EntityManager.AddComponent<NT_SelectedFirst>(entity);
                    break;

                case SelectionState.StartNodeSelected:
                    m_Log.Debug("→ EndNodeSelected");
                    m_SelectedNodes.Add(entity);
                    EntityManager.AddComponentData(entity, NT_Selected.DefaultNode);
                    EntityManager.AddComponent<NT_SelectedLast>(entity);
                    break;

                case SelectionState.EndNodeSelected:
                    m_Log.Debug("→ Extending path");

                    // Remove SelectedLast from old endpoint
                    var previousEnd = m_SelectedNodes[^1];
                    EntityManager.RemoveComponent<NT_SelectedLast>(previousEnd);

                    // Add new endpoint
                    m_SelectedNodes.Add(entity);
                    EntityManager.AddComponentData(entity, NT_Selected.DefaultNode);
                    EntityManager.AddComponent<NT_SelectedLast>(entity);
                    break;
            }

            // 2. Merge preview path into persistent path
            CommitNextPathToCurrentPath();

            // 3. Update path indices for rendering
            UpdatePathIndices();

            // 4. Recalculate eligible nodes from new endpoint
            RecalculateEligibleNodes(entity);

            return true;
        }

        /// <summary>
        /// Merges the hovered preview path (m_NextPathNodes/Edges) into the committed path.
        /// Called when a node is added — this confirms the hover preview.
        /// </summary>
        private void CommitNextPathToCurrentPath() {
            // Add new path nodes
            foreach (var node in m_NextPathNodes) {
                if (!m_CurrentPathNodes.Contains(node)) {
                    m_CurrentPathNodes.Add(node);
                    EntityManager.AddComponentData(node, NT_Selected.DefaultNode);
                }
            }

            // Add new path edges
            foreach (var edge in m_NextPathEdges) {
                if (!m_CurrentPathEdges.Contains(edge)) {
                    m_CurrentPathEdges.Add(edge);
                    EntityManager.AddComponentData(edge, NT_Selected.DefaultEdge);
                }
            }

            // Clear preview (will be rebuilt on next hover)
            m_NextPathNodes.Clear();
            m_NextPathEdges.Clear();

            m_Log.Debug($"CommitNextPathToCurrentPath: Path now {m_CurrentPathNodes.Length} nodes, {m_CurrentPathEdges.Length} edges");
        }

        /// <summary>
        /// Recalculates which nodes are eligible for selection from the current endpoint.
        /// Replaces NT_Eligible on all nodes with only those reachable from the endpoint.
        /// </summary>
        private void RecalculateEligibleNodes(Entity fromNode) {
            // Clear all NT_Eligible
            EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);

            // Find new eligible nodes
            m_EligibleNodes.Clear();
            FindEligibleNodes(fromNode, m_EligibleNodes);

            // Add NT_Eligible to newly eligible nodes
            var eligibleArray = new NativeArray<Entity>(m_EligibleNodes.AsArray(), Allocator.Temp);
            EntityManager.AddComponent<NT_Eligible>(eligibleArray);

            m_Log.Debug($"RecalculateEligibleNodes: {m_EligibleNodes.Length} nodes reachable from endpoint");
        }

        /// <summary>
        /// Removes the last node from the path. Returns true if selection changed.
        /// Handles backtracking through all states.
        /// </summary>
        private bool HandleRemoveNode() {
            if (m_SelectedNodes.Length == 0) {
                return false;
            }

            var lastNode = m_SelectedNodes[^1];
            m_Log.Debug($"[{CurrentSelectionState}] Removing node: {lastNode}");

            switch (CurrentSelectionState) {
                case SelectionState.NoSelection:
                    return false;

                case SelectionState.StartNodeSelected:
                    m_Log.Debug("→ NoSelection");
                    EntityManager.RemoveComponent<NT_Selected>(lastNode);
                    EntityManager.RemoveComponent<NT_SelectedFirst>(lastNode);
                    m_SelectedNodes.RemoveAt(m_SelectedNodes.Length - 1);
                    StateTransitionNoNodes();
                    return true;

                case SelectionState.EndNodeSelected:
                    m_Log.Debug("→ Trimming path");

                    // Remove endpoint marker from current last node
                    EntityManager.RemoveComponent<NT_Selected>(lastNode);
                    EntityManager.RemoveComponent<NT_SelectedLast>(lastNode);
                    m_SelectedNodes.RemoveAt(m_SelectedNodes.Length - 1);

                    // Trim path nodes/edges back to the new endpoint
                    var newEndNode = m_SelectedNodes[^1];
                    TrimPathToNode(newEndNode);

                    // Update rendering indices
                    UpdatePathIndices();

                    // Recalculate eligible nodes from new endpoint
                    RecalculateEligibleNodes(newEndNode);

                    // If back to single node, it's still SelectedFirst (no change needed)
                    // If still have 2+, mark the new last node
                    if (m_SelectedNodes.Length >= 2) {
                        EntityManager.AddComponent<NT_SelectedLast>(newEndNode);
                    }

                    return true;
            }

            return false;
        }

        /// <summary>
        /// Removes path nodes and edges from the end back to (and including) targetNode.
        /// Used when backing up the path via right-click.
        /// </summary>
        private void TrimPathToNode(Entity targetNode) {
            var trimmedNodes = 0;
            var trimmedEdges = 0;

            var done = false;
            while (!done) {
                // Stop if we've reached the target or emptied the path
                if (m_CurrentPathNodes.Length == 0 || m_CurrentPathNodes[^1] == targetNode) {
                    done = true;
                    break;
                }

                // Remove trailing node
                var nodeToRemove = m_CurrentPathNodes[^1];
                EntityManager.RemoveComponent<NT_Selected>(nodeToRemove);
                m_CurrentPathNodes.RemoveAt(m_CurrentPathNodes.Length - 1);
                trimmedNodes++;

                // Remove trailing edge
                if (m_CurrentPathEdges.Length > 0) {
                    var edgeToRemove = m_CurrentPathEdges[^1];
                    EntityManager.RemoveComponent<NT_Selected>(edgeToRemove);
                    m_CurrentPathEdges.RemoveAt(m_CurrentPathEdges.Length - 1);
                    trimmedEdges++;
                }
            }

            m_Log.Debug($"TrimPathToNode: Removed {trimmedNodes} nodes, {trimmedEdges} edges");
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

            // Destroy any active handles
            DestroyAllHandles();

            // Batch remove all marker components using cached queries
            EntityManager.RemoveComponent<NT_Selected>(m_NodesWithSelectedQuery);
            EntityManager.RemoveComponent<NT_Selected>(m_EdgesWithSelectedQuery);
            EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);
            EntityManager
                .RemoveComponent<NT_SelectedFirst>(m_NodesWithSelectedFirstQuery);
            EntityManager
                .RemoveComponent<NT_SelectedLast>(m_NodesWithSelectedLastQuery);

            ClearAllHighlights();

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