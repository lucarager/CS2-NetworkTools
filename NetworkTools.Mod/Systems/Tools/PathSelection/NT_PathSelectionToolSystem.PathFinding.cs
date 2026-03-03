// <copyright file="NT_PathSelectionToolSystem.PathFinding.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using Colossal.Entities;
    using Game.Net;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    ///     Partial class containing pathfinding logic for node selection.
    /// </summary>
    public abstract partial class NT_PathSelectionToolSystem {
        /// <summary>
        ///     Finds all nodes eligible for selection from a starting node.
        ///     Traverses in all directions until hitting intersections (>2 edges) or road ends.
        ///     The start node itself is always included, even if it's an intersection.
        ///     Skips nodes that are already in the current path to avoid backing up.
        /// </summary>
        /// <param name="startNode">The node to start traversal from.</param>
        /// <param name="outEligibleNodes">Output list of eligible nodes.</param>
        protected void FindEligibleNodes(Entity startNode, NativeList<Entity> outEligibleNodes) {
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

                    var neighbor = edge.m_Start == currentEntity ? edge.m_End : edge.m_Start;

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

            m_Log.Debug(
                $"FindPathBetween: Found path with {nodesPath.Length} nodes and {edgePath.Length} edges: {foundPath}");
            return foundPath;
        }
    }
}
