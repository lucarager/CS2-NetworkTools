namespace NetworkTools.Systems {
    using System;
    using System.Collections.Generic;

    using Colossal.Entities;

    using Game.Net;
    using Game.Prefabs;
    using Game.Simulation;

    using Unity.Entities;

    /// <summary>
    ///     Categories of electricity flow-graph corruption the verifier can detect.
    /// </summary>
    public enum ElectricityIssueKind {
        InvalidNode,
        MissingBuffer,
        CorruptBuffer,
        MissingNodeConnection,
        MissingFlowEdge,
    }

    /// <summary>
    ///     A single detected corruption, paired with a human-readable message.
    /// </summary>
    public readonly struct ElectricityIssue {
        public readonly ElectricityIssueKind Kind;
        public readonly string Message;

        public ElectricityIssue(ElectricityIssueKind kind, string message) {
            Kind = kind;
            Message = message;
        }
    }

    /// <summary>
    ///     Shared, read-only validation of the electricity flow graph for a single net edge.
    ///     Mirrors both the game's deserialize-time <c>ElectricityGraphSystem</c> and the runtime
    ///     <c>ElectricityEdgeGraphSystem</c>: every endpoint and connected (middle) node must own a
    ///     flow edge to the edge's middle flow node, and every flow node's
    ///     <see cref="ConnectedFlowEdge"/> buffer must be structurally intact.
    ///     Both <c>NT_DebugSystem</c> (deserialize) and <c>NT_ElectricityWatchdogSystem</c> (runtime)
    ///     consume this so the two checkers can never drift apart.
    /// </summary>
    public static class ElectricityGraphVerifier {

        /// <summary>
        ///     Appends every issue found for <paramref name="edge"/> to <paramref name="issues"/>.
        ///     <paramref name="nodeHealth"/> caches per-node structural health for the current pass so
        ///     a shared endpoint is validated (and reported) once — pass the same dictionary for every
        ///     edge in a scan and clear it between scans.
        /// </summary>
        public static void VerifyEdge(EntityManager em, Entity edge,
                                      Dictionary<Entity, bool> nodeHealth, List<ElectricityIssue> issues) {
            if (!em.TryGetComponent<Edge>(edge, out var edgeData) ||
                !em.TryGetComponent<ElectricityNodeConnection>(edge, out var edgeConnection))
                return;

            var middleNode = edgeConnection.m_ElectricityNode;
            var middleHealthy = ValidateElectricityNode(em, middleNode, $"Edge {edge.Index} middle node", nodeHealth, issues);

            CheckEndpoint(em, edge, edgeData.m_Start, middleNode, middleHealthy, "start", nodeHealth, issues);
            CheckEndpoint(em, edge, edgeData.m_End, middleNode, middleHealthy, "end", nodeHealth, issues);

            if (em.TryGetBuffer<ConnectedNode>(edge, true, out var connectedNodes)) {
                foreach (var connectedNode in connectedNodes) {
                    CheckConnectedNode(em, connectedNode.m_Node, middleNode, middleHealthy, nodeHealth, issues);
                }
            }
        }

        /// <summary>
        ///     Validates one of the edge's endpoints: it must carry an
        ///     <see cref="ElectricityNodeConnection"/> (the game indexes this directly and would read
        ///     garbage in Burst if absent), its electricity node must be structurally healthy, and a
        ///     flow edge must link it to <paramref name="middleNode"/>.
        /// </summary>
        private static void CheckEndpoint(EntityManager em, Entity edge, Entity endpoint, Entity middleNode,
                                          bool middleHealthy, string side,
                                          Dictionary<Entity, bool> nodeHealth, List<ElectricityIssue> issues) {
            if (!em.TryGetComponent<ElectricityNodeConnection>(endpoint, out var connection)) {
                issues.Add(new ElectricityIssue(ElectricityIssueKind.MissingNodeConnection,
                    $"Edge {edge.Index} {side} endpoint {endpoint} MISSING ElectricityNodeConnection — game reads garbage in Burst!"));
                return;
            }

            var endpointNode = connection.m_ElectricityNode;
            var endpointHealthy = ValidateElectricityNode(em, endpointNode, $"Edge {edge.Index} {side} endpoint", nodeHealth, issues);

            if (middleHealthy && endpointHealthy && !HasFlowEdgeBetween(em, endpointNode, middleNode)) {
                issues.Add(new ElectricityIssue(ElectricityIssueKind.MissingFlowEdge,
                    $"ElectricityFlowEdge for net edge {edge.Index} not found! ({side} endpoint {endpoint}, electricity node {endpointNode} <-> middle {middleNode})"));
            }
        }

        /// <summary>
        ///     Validates one connected (middle) node behind the same gate the game uses (PrefabRef +
        ///     ElectricityConnectionData), then checks its electricity node for structural health and
        ///     a flow edge linking it to <paramref name="middleNode"/>.
        /// </summary>
        private static void CheckConnectedNode(EntityManager em, Entity node, Entity middleNode, bool middleHealthy,
                                               Dictionary<Entity, bool> nodeHealth, List<ElectricityIssue> issues) {
            // If these fail the game also skips this node, so no flow edge is expected.
            if (!em.TryGetComponent<PrefabRef>(node, out var prefabRef) ||
                !em.HasComponent<ElectricityConnectionData>(prefabRef.m_Prefab))
                return;

            if (!em.TryGetComponent<ElectricityNodeConnection>(node, out var connection)) {
                issues.Add(new ElectricityIssue(ElectricityIssueKind.MissingNodeConnection,
                    $"Connected node {node.Index} has ElectricityConnectionData but MISSING ElectricityNodeConnection — game reads garbage in Burst!"));
                return;
            }

            var nodeElec = connection.m_ElectricityNode;
            var nodeHealthy = ValidateElectricityNode(em, nodeElec, $"Connected node {node.Index}", nodeHealth, issues);

            if (middleHealthy && nodeHealthy && !HasFlowEdgeBetween(em, nodeElec, middleNode)) {
                issues.Add(new ElectricityIssue(ElectricityIssueKind.MissingFlowEdge,
                    $"ElectricityFlowEdge for connected node {node.Index} not found! (electricity node {nodeElec} <-> middle {middleNode})"));
            }
        }

        /// <summary>
        ///     Structural health of an electricity node: a valid, living entity that owns a readable
        ///     <see cref="ConnectedFlowEdge"/> buffer pointing only at living flow edges. The verdict
        ///     is cached in <paramref name="nodeHealth"/>; the first encounter records an issue, later
        ///     encounters of a shared node return the cached verdict without re-reporting.
        /// </summary>
        public static bool ValidateElectricityNode(EntityManager em, Entity node, string context,
                                                   Dictionary<Entity, bool> nodeHealth, List<ElectricityIssue> issues) {
            if (nodeHealth.TryGetValue(node, out var cached))
                return cached;

            var healthy = true;

            if (node.Index <= 0 || !em.Exists(node)) {
                issues.Add(new ElectricityIssue(ElectricityIssueKind.InvalidNode,
                    $"{context}: invalid/stale electricity node {node}"));
                healthy = false;
            } else if (!em.HasBuffer<ConnectedFlowEdge>(node)) {
                issues.Add(new ElectricityIssue(ElectricityIssueKind.MissingBuffer,
                    $"{context}: electricity node {node} missing ConnectedFlowEdge buffer"));
                healthy = false;
            } else if (!ValidateFlowEdgeBuffer(em, node)) {
                issues.Add(new ElectricityIssue(ElectricityIssueKind.CorruptBuffer,
                    $"{context}: electricity node {node} has corrupt ConnectedFlowEdge buffer"));
                healthy = false;
            }

            nodeHealth[node] = healthy;
            return healthy;
        }

        /// <summary>
        ///     Iterates a node's <see cref="ConnectedFlowEdge"/> buffer and verifies every referenced
        ///     edge entity carries a readable <see cref="ElectricityFlowEdge"/> whose
        ///     <c>m_Start</c>/<c>m_End</c> still reference living entities. Returns <c>false</c> when
        ///     the buffer is unreadable, references stale edges, or its flow edges point at destroyed
        ///     nodes. <see cref="EntityManager.HasBuffer{T}(Entity)"/> only checks the archetype; the
        ///     backing pointer can still be null/freed — the NullRef class of corruption.
        /// </summary>
        private static bool ValidateFlowEdgeBuffer(EntityManager em, Entity node) {
            try {
                var buffer = em.GetBuffer<ConnectedFlowEdge>(node, true);
                foreach (var connectedEdge in buffer) {
                    if (!em.Exists(connectedEdge.m_Edge) ||
                        !em.HasComponent<ElectricityFlowEdge>(connectedEdge.m_Edge))
                        return false;

                    var flowEdge = em.GetComponentData<ElectricityFlowEdge>(connectedEdge.m_Edge);
                    if (!em.Exists(flowEdge.m_Start) ||
                        !em.Exists(flowEdge.m_End))
                        return false;
                }
                return true;
            } catch (Exception) {
                return false;
            }
        }

        /// <summary>
        ///     Mirrors the game's <c>UpdateFlowEdge</c>: a flow edge links the two nodes if either
        ///     <paramref name="startNode"/>'s buffer holds an edge with (m_Start==startNode,
        ///     m_End==endNode) or <paramref name="endNode"/>'s buffer holds the reverse.
        /// </summary>
        public static bool HasFlowEdgeBetween(EntityManager em, Entity startNode, Entity endNode) {
            return FindFlowEdge(em, startNode, startNode, endNode) ||
                   FindFlowEdge(em, endNode, endNode, startNode);
        }

        private static bool FindFlowEdge(EntityManager em, Entity bufferNode, Entity wantStart, Entity wantEnd) {
            if (!em.TryGetBuffer<ConnectedFlowEdge>(bufferNode, true, out var buffer))
                return false;

            foreach (var connectedFlowEdge in buffer) {
                if (em.TryGetComponent<ElectricityFlowEdge>(connectedFlowEdge.m_Edge, out var flowEdge) &&
                    flowEdge.m_Start == wantStart &&
                    flowEdge.m_End == wantEnd) {
                    return true;
                }
            }
            return false;
        }
    }
}
