// <copyright file="NT_PathSelectionToolSystem.PathFinding.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using Colossal.Collections;
    using Colossal.Entities;
    using Game.Net;
    using Game.Prefabs;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    ///     Partial class containing pathfinding logic for node selection.
    /// </summary>
    public abstract partial class NT_PathSelectionToolSystem {
        /// <summary>
        ///     Finds all nodes eligible for selection reachable from a starting node.
        ///     Traverses the full connected network in all directions until road ends.
        ///     The start node itself is excluded from the results.
        ///     Skips nodes that are already in the current path to avoid backing up.
        /// </summary>
        /// <param name="startNode">The node to start traversal from.</param>
        /// <param name="outEligibleNodes">Output list of eligible nodes.</param>
        protected void FindEligibleNodes(Entity startNode, NativeList<Entity> outEligibleNodes) {
            outEligibleNodes.Clear();

            var toVisit = new NativeQueue<Entity>(Allocator.Temp);
            var visited = new NativeHashSet<Entity>(64, Allocator.Temp);

            toVisit.Enqueue(startNode);
            visited.Add(startNode);

            while (toVisit.TryDequeue(out var current)) {
                // Get connected edges
                if (!EntityManager.HasBuffer<ConnectedEdge>(current)) {
                    continue;
                }

                var connectedEdges = EntityManager.GetBuffer<ConnectedEdge>(current);

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
        ///     Heap entry for the weighted path search, ordered by accumulated cost.
        /// </summary>
        private struct PathCandidate : ILessThan<NT_PathSelectionToolSystem.PathCandidate> {
            public Entity m_Node;   // Node reached.
            public Entity m_Edge;   // Edge traversed to reach m_Node.
            public Entity m_Parent; // Node we arrived from.
            public float m_Cost;    // Accumulated cost to reach m_Node.

            public bool LessThan(NT_PathSelectionToolSystem.PathCandidate other) {
                return m_Cost < other.m_Cost;
            }
        }

        /// <summary>
        ///     Cost added when the path switches to a different network prefab. Biases the search
        ///     toward staying on the same road type, mirroring NetToolSystem.CreatePath's 9.9f.
        /// </summary>
        private const float k_PrefabChangePenalty = 9.9f;

        /// <summary>
        ///     Finds the lowest-cost path between two nodes using a weighted (Dijkstra) search.
        ///     Cost is the summed edge length plus a penalty each time the road type changes, so
        ///     the path prefers the geometrically shorter route and stays on the same network.
        ///     Returns the path including start and end nodes, and the edges connecting them.
        /// </summary>
        /// <param name="startNode">Starting node.</param>
        /// <param name="endNode">Ending node.</param>
        /// <param name="nodesPath">Output list containing the path from start to end.</param>
        /// <param name="edgePath">Output list containing the edges in the path.</param>
        /// <returns>True if a path was found, false otherwise.</returns>
        protected bool FindPathBetween(
            Entity startNode,
            Entity endNode,
            ref NativeList<Entity> nodesPath,
            ref NativeList<Entity> edgePath) {

            nodesPath.Clear();
            edgePath.Clear();

            if (startNode == endNode) {
                return true;
            }

            var heap = new NativeMinHeap<NT_PathSelectionToolSystem.PathCandidate>(64, Allocator.Temp);
            var visited = new NativeHashSet<Entity>(64, Allocator.Temp);
            var parentMap = new NativeHashMap<Entity, Entity>(64, Allocator.Temp);
            var edgeMap = new NativeHashMap<Entity, Entity>(64, Allocator.Temp);

            heap.Insert(new NT_PathSelectionToolSystem.PathCandidate {
                m_Node = startNode,
                m_Edge = Entity.Null,
                m_Parent = Entity.Null,
                m_Cost = 0f,
            });

            var foundPath = false;

            while (heap.Length != 0) {
                var current = heap.Extract();

                // A node can be queued via several routes; the first one extracted is the
                // cheapest, so settle it once and ignore any later, costlier arrivals.
                if (!visited.Add(current.m_Node)) {
                    continue;
                }

                // Record how we reached this node for path reconstruction.
                if (current.m_Node != startNode) {
                    parentMap[current.m_Node] = current.m_Parent;
                    edgeMap[current.m_Node] = current.m_Edge;
                }

                if (current.m_Node == endNode) {
                    foundPath = true;
                    break;
                }

                if (!EntityManager.TryGetBuffer<ConnectedEdge>(current.m_Node, true, out var connectedEdges)) {
                    continue;
                }

                // Prefab of the edge we arrived on, used for the road-type-change penalty.
                var arrivedPrefab = Entity.Null;
                if (current.m_Edge != Entity.Null &&
                    EntityManager.TryGetComponent<PrefabRef>(current.m_Edge, out var arrivedRef)) {
                    arrivedPrefab = arrivedRef.m_Prefab;
                }

                // Expand neighbors in both directions.
                for (var i = 0; i < connectedEdges.Length; i++) {
                    var edgeEntity = connectedEdges[i].m_Edge;

                    // Don't immediately backtrack along the edge we just took.
                    if (edgeEntity == current.m_Edge) {
                        continue;
                    }

                    if (!EntityManager.TryGetComponent<Edge>(edgeEntity, out var edge)) {
                        continue;
                    }

                    var neighbor = edge.m_Start == current.m_Node ? edge.m_End : edge.m_Start;

                    if (visited.Contains(neighbor)) {
                        continue;
                    }

                    var cost = current.m_Cost;

                    if (EntityManager.TryGetComponent<Curve>(edgeEntity, out var curve)) {
                        cost += curve.m_Length;
                    }

                    if (arrivedPrefab != Entity.Null &&
                        EntityManager.TryGetComponent<PrefabRef>(edgeEntity, out var edgeRef) &&
                        edgeRef.m_Prefab != arrivedPrefab) {
                        cost += k_PrefabChangePenalty;
                    }

                    heap.Insert(new NT_PathSelectionToolSystem.PathCandidate {
                        m_Node = neighbor,
                        m_Edge = edgeEntity,
                        m_Parent = current.m_Node,
                        m_Cost = cost,
                    });
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

            heap.Dispose();
            visited.Dispose();
            parentMap.Dispose();
            edgeMap.Dispose();

            m_Log.Debug(
                $"FindPathBetween: Found path with {nodesPath.Length} nodes and {edgePath.Length} edges: {foundPath}");
            return foundPath;
        }
    }
}
