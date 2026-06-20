namespace NetworkTools.Systems {
    using System;
    using System.Collections.Generic;

    using Colossal.Entities;

    using Game;
    using Game.Net;
    using Game.Prefabs;
    using Game.Simulation;

    using LucaModsCommon.Utils;

    using Unity.Collections;
    using Unity.Entities;

    public partial class NT_DebugSystem : GameSystemBase {

        /// <summary>
        ///     Logger instance
        /// </summary>
        private PrefixedLogger m_Log;
        private EntityQuery m_EntitiesToProcessQuery;
        private EntityArchetype m_RoadConnectionEventArchetype;

        /// <summary>
        ///     Per-pass cache of electricity-node structural health, keyed by node entity.
        ///     Endpoints are shared between edges, so this both de-duplicates the logged
        ///     corruption and avoids re-walking the same buffer repeatedly.
        /// </summary>
        private readonly Dictionary<Entity, bool> m_NodeHealth = new Dictionary<Entity, bool>();

        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(NT_DebugSystem));

            m_EntitiesToProcessQuery = SystemAPI.QueryBuilder()
                                                .WithAll<Game.Net.ElectricityConnection, ElectricityNodeConnection, Edge, PrefabRef> ()
                                                .Build();
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            var entities = m_EntitiesToProcessQuery.ToEntityArray(Allocator.Temp);
            m_Log.Debug($"Verifying electricity connections... ({entities.Length} edges)");

            m_NodeHealth.Clear();
            var tally = default(Tally);

            foreach (var entity in entities)
            {
                var edge = EntityManager.GetComponentData<Edge>(entity);
                var middleNode = EntityManager.GetComponentData<ElectricityNodeConnection>(entity).m_ElectricityNode;

                // 1. The edge's own middle node must be a structurally healthy flow node — it is
                //    the shared endpoint of every flow edge this edge is responsible for.
                var middleHealthy = ValidateElectricityNode(middleNode, $"Edge {entity.Index} middle node", ref tally);

                // 2. Each of the edge's endpoints must have an electricity node that is healthy
                //    and linked to the middle node (mirrors EdgeJob.Execute).
                CheckEndpoint(entity, edge.m_Start, middleNode, middleHealthy, "start", ref tally);
                CheckEndpoint(entity, edge.m_End, middleNode, middleHealthy, "end", ref tally);

                // 3. Each connected (middle) node must have an electricity node that is healthy
                //    and linked to the middle node (mirrors UpdateEdgeMiddleNodeConnections).
                if (EntityManager.TryGetBuffer<ConnectedNode>(entity, true, out var connectedNodes))
                {
                    foreach (var connectedNode in connectedNodes)
                    {
                        CheckConnectedNode(connectedNode.m_Node, middleNode, middleHealthy, ref tally);
                    }
                }
            }

            m_Log.Debug(
                $"Verification complete across {entities.Length} edges. {tally.Total} issue(s): " +
                $"{tally.InvalidNodes} invalid/stale node(s), {tally.MissingBuffers} missing buffer(s), " +
                $"{tally.CorruptBuffers} corrupt buffer(s), {tally.MissingNodeConnections} missing node connection(s), " +
                $"{tally.MissingFlowEdges} missing flow edge(s).");
        }

        /// <summary>
        ///     Validates one of the edge's endpoints: it must carry an
        ///     <see cref="ElectricityNodeConnection"/> (the game indexes this directly and would
        ///     read garbage in Burst if absent), its electricity node must be structurally healthy,
        ///     and a flow edge must link it to <paramref name="middleNode"/>.
        /// </summary>
        private void CheckEndpoint(Entity edgeEntity, Entity endpoint, Entity middleNode, bool middleHealthy,
                                   string side, ref Tally tally) {
            if (!EntityManager.TryGetComponent<ElectricityNodeConnection>(endpoint, out var connection)) {
                m_Log.Debug($"Edge {edgeEntity.Index} {side} endpoint {endpoint} MISSING ElectricityNodeConnection — game reads garbage in Burst!");
                tally.MissingNodeConnections++;
                return;
            }

            var endpointNode = connection.m_ElectricityNode;
            var endpointHealthy = ValidateElectricityNode(endpointNode, $"Edge {edgeEntity.Index} {side} endpoint", ref tally);

            // A missing-link report is only meaningful when both ends are healthy flow nodes;
            // otherwise the corrupt-buffer report above is the real (and already counted) issue.
            if (middleHealthy && endpointHealthy && !HasFlowEdgeBetween(endpointNode, middleNode)) {
                m_Log.Debug($"ElectricityFlowEdge for net edge {edgeEntity.Index} not found! ({side} endpoint {endpoint}, electricity node {endpointNode} <-> middle {middleNode})");
                tally.MissingFlowEdges++;
            }
        }

        /// <summary>
        ///     Validates one connected (middle) node behind the same gate the game uses
        ///     (PrefabRef + ElectricityConnectionData), then checks its electricity node for
        ///     structural health and a flow edge linking it to <paramref name="middleNode"/>.
        /// </summary>
        private void CheckConnectedNode(Entity node, Entity middleNode, bool middleHealthy, ref Tally tally) {
            // If these fail the game also skips this node, so no warning is expected here.
            if (!EntityManager.TryGetComponent<PrefabRef>(node, out var prefabRef) ||
                !EntityManager.HasComponent<ElectricityConnectionData>(prefabRef.m_Prefab))
                return;

            // The game directly indexes m_ElectricityNodeConnections[node]; a missing component
            // reads garbage under Burst. Surface it instead of throwing.
            if (!EntityManager.TryGetComponent<ElectricityNodeConnection>(node, out var connection)) {
                m_Log.Debug($"Connected node {node.Index} has ElectricityConnectionData but MISSING ElectricityNodeConnection — game reads garbage in Burst!");
                tally.MissingNodeConnections++;
                return;
            }

            var nodeElec = connection.m_ElectricityNode;
            var nodeHealthy = ValidateElectricityNode(nodeElec, $"Connected node {node.Index}", ref tally);

            if (middleHealthy && nodeHealthy && !HasFlowEdgeBetween(nodeElec, middleNode)) {
                m_Log.Debug($"ElectricityFlowEdge for connected node {node.Index} not found! (electricity node {nodeElec} <-> middle {middleNode})");
                tally.MissingFlowEdges++;
            }
        }

        /// <summary>
        ///     Structural health of an electricity node: a valid, living entity that owns a
        ///     readable <see cref="ConnectedFlowEdge"/> buffer pointing only at living flow edges.
        ///     Results are cached for the current pass; the first encounter logs and counts the
        ///     issue, later encounters of a shared node return the cached verdict silently.
        /// </summary>
        private bool ValidateElectricityNode(Entity node, string context, ref Tally tally) {
            if (m_NodeHealth.TryGetValue(node, out var cached))
                return cached;

            var healthy = true;

            if (node.Index <= 0 || !EntityManager.Exists(node)) {
                m_Log.Debug($"{context}: invalid/stale electricity node {node}");
                tally.InvalidNodes++;
                healthy = false;
            } else if (!EntityManager.HasBuffer<ConnectedFlowEdge>(node)) {
                m_Log.Debug($"{context}: electricity node {node} missing ConnectedFlowEdge buffer");
                tally.MissingBuffers++;
                healthy = false;
            } else if (!ValidateFlowEdgeBuffer(node)) {
                m_Log.Debug($"{context}: electricity node {node} has corrupt ConnectedFlowEdge buffer");
                tally.CorruptBuffers++;
                healthy = false;
            }

            m_NodeHealth[node] = healthy;
            return healthy;
        }

        /// <summary>
        ///     Attempts to iterate a node's <see cref="ConnectedFlowEdge"/> buffer and verify every
        ///     referenced edge entity carries a readable <see cref="ElectricityFlowEdge"/> whose
        ///     <c>m_Start</c>/<c>m_End</c> still reference living entities. Returns <c>false</c> when
        ///     the buffer is unreadable, references stale edges, or its flow edges point at destroyed
        ///     nodes. <see cref="EntityManager.HasBuffer{T}"/> only checks the archetype; the backing
        ///     array pointer can still be null/freed, which is the NullRef class of corruption.
        /// </summary>
        private bool ValidateFlowEdgeBuffer(Entity node) {
            try {
                var buffer = EntityManager.GetBuffer<ConnectedFlowEdge>(node, true);
                foreach (var connectedEdge in buffer) {
                    if (!EntityManager.Exists(connectedEdge.m_Edge) ||
                        !EntityManager.HasComponent<ElectricityFlowEdge>(connectedEdge.m_Edge))
                        return false;

                    // Mirrors flowEdges[entity] + the m_Start/m_End reads in TryGetFlowEdge.
                    var flowEdge = EntityManager.GetComponentData<ElectricityFlowEdge>(connectedEdge.m_Edge);
                    if (!EntityManager.Exists(flowEdge.m_Start) ||
                        !EntityManager.Exists(flowEdge.m_End))
                        return false;
                }
                return true;
            } catch (Exception) {
                // Buffer exists on the archetype but the backing data is corrupted.
                return false;
            }
        }

        /// <summary>
        ///     Mirrors <c>UpdateFlowEdge</c>: a flow edge linking the two nodes exists if either
        ///     <paramref name="startNode"/>'s buffer holds an edge with (m_Start==startNode,
        ///     m_End==endNode), or <paramref name="endNode"/>'s buffer holds the reverse. When
        ///     neither is found the game logs "ElectricityFlowEdge ... not found!".
        /// </summary>
        private bool HasFlowEdgeBetween(Entity startNode, Entity endNode) {
            return FindFlowEdge(startNode, startNode, endNode) ||
                   FindFlowEdge(endNode, endNode, startNode);
        }

        /// <summary>
        ///     Walks <paramref name="bufferNode"/>'s <see cref="ConnectedFlowEdge"/> buffer looking
        ///     for an <see cref="ElectricityFlowEdge"/> whose <c>m_Start</c>/<c>m_End</c> match the
        ///     requested pair — the same comparison done in <c>TryGetFlowEdge</c>.
        /// </summary>
        private bool FindFlowEdge(Entity bufferNode, Entity wantStart, Entity wantEnd) {
            if (!EntityManager.TryGetBuffer<ConnectedFlowEdge>(bufferNode, true, out var buffer))
                return false;

            foreach (var connectedFlowEdge in buffer) {
                if (EntityManager.TryGetComponent<ElectricityFlowEdge>(connectedFlowEdge.m_Edge, out var flowEdge) &&
                    flowEdge.m_Start == wantStart &&
                    flowEdge.m_End == wantEnd) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        ///     Running count of each corruption category for one verification pass.
        /// </summary>
        private struct Tally {
            public int InvalidNodes;
            public int MissingBuffers;
            public int CorruptBuffers;
            public int MissingNodeConnections;
            public int MissingFlowEdges;

            public int Total =>
                InvalidNodes + MissingBuffers + CorruptBuffers + MissingNodeConnections + MissingFlowEdges;
        }
    }
}
