namespace NetworkTools.Systems {
    using System;

    using Colossal.Entities;

    using Game;
    using Game.Net;
    using Game.Prefabs;
    using Game.Serialization;
    using Game.Simulation;

    using NetworkTools.Utils;

    using Unity.Collections;
    using Unity.Entities;

    public partial class NT_DebugSystem : GameSystemBase {

        /// <summary>
        ///     Logger instance
        /// </summary>
        private PrefixedLogger m_Log;
        private EntityQuery m_EntitiesToProcessQuery;
        private EntityArchetype m_RoadConnectionEventArchetype;

        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(NT_DebugSystem));

            m_EntitiesToProcessQuery = SystemAPI.QueryBuilder().WithAll<Game.Net.ElectricityConnection, ElectricityNodeConnection, Game.Net.Edge, PrefabRef> ().Build();
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            var entities = m_EntitiesToProcessQuery.ToEntityArray(Allocator.Temp);
            m_Log.Debug($"Verifying electricity connections... ({entities.Length} edges)");

            var issuesFound = 0;
            foreach (var entity in entities)
            {
                var edge = EntityManager.GetComponentData<Edge>(entity);
                var electricityNodeConnection = EntityManager.GetComponentData<ElectricityNodeConnection>(entity);

                var electricityNode3 = electricityNodeConnection.m_ElectricityNode;

                // Validate the edge's own electricity node first — it is passed as
                // endNode to every FindBrokenFlowEdgeBuffer call inside CheckNodes.
                if (electricityNode3.Index <= 0 || !EntityManager.Exists(electricityNode3)) {
                    m_Log.Debug($"Edge {entity} has invalid/stale electricity node {electricityNode3}");
                    issuesFound++;
                    continue;
                }

                if (!EntityManager.HasBuffer<ConnectedFlowEdge>(electricityNode3) ||
                    !ValidateFlowEdgeBuffer(electricityNode3)) {
                    m_Log.Debug($"Edge {entity} has broken ConnectedFlowEdge buffer on electricity node {electricityNode3}");
                    issuesFound++;
                    continue;
                }

                if (EntityManager.TryGetBuffer<ConnectedNode>(entity, true, out var connectedNodes) &&
                    connectedNodes.Length != 0)
                {
                    issuesFound += CheckNodes(entity, connectedNodes, electricityNode3);
                }
            }

            m_Log.Debug($"Verification complete. {issuesFound} issue(s) found across {entities.Length} edges.");
        }

        private int CheckNodes(Entity edgeEntity, DynamicBuffer<ConnectedNode> connectedNodes,
        Entity flowMiddleNode) {
            var issues = 0;
            foreach (var connectedNode in connectedNodes)
            {
                // Mirror the game code's gate: PrefabRef + ElectricityConnectionData on the prefab.
                // If these fail, the game also skips this node — no crash path.
                if (!EntityManager.TryGetComponent<PrefabRef>(connectedNode.m_Node, out var prefabRef) ||
                    !EntityManager.TryGetComponent<ElectricityConnectionData>(prefabRef.m_Prefab,
                                                                              out var electricityConnectionData))
                    continue;

                // The game does NOT TryGet ElectricityNodeConnection — it directly indexes:
                //   m_ElectricityNodeConnections[connectedNode.m_Node].m_ElectricityNode
                // In Burst with safety checks stripped, a missing component reads garbage,
                // producing a junk entity whose ConnectedFlowEdge buffer pointer is null → NullRef.
                if (!EntityManager.TryGetComponent<ElectricityNodeConnection>(connectedNode.m_Node,
                                                                              out var electricityNodeConnection)) {
                    m_Log.Debug($"Connected node {connectedNode.m_Node} on edge {edgeEntity} has ElectricityConnectionData but MISSING ElectricityNodeConnection — game will read garbage in Burst!");
                    issues++;
                    continue;
                }

                if (FindBrokenFlowEdgeBuffer(electricityNodeConnection.m_ElectricityNode,
                                  flowMiddleNode,
                                  electricityConnectionData)) {
                    m_Log.Debug($"Broken ElectricityFlowEdge found on edge {edgeEntity} for start node {electricityNodeConnection.m_ElectricityNode} with end node {flowMiddleNode}");
                    issues++;
                }
            }
            return issues;
        }

        private bool FindBrokenFlowEdgeBuffer(Entity                    startNode,
                               Entity                    endNode,
                               ElectricityConnectionData connectionData) {
            // Check entity indices
            if (startNode.Index <= 0 || endNode.Index <= 0)
                return true;

            // Check entities still exist (index could be > 0 but entity destroyed / version mismatch)
            if (!EntityManager.Exists(startNode) || !EntityManager.Exists(endNode))
                return true;

            // Check buffer component existence
            if (!EntityManager.HasBuffer<ConnectedFlowEdge>(startNode) ||
                !EntityManager.HasBuffer<ConnectedFlowEdge>(endNode))
                return true;

            // Validate buffer contents are readable and reference valid flow edges.
            // HasBuffer only checks the archetype; the backing array pointer can still
            // be null/freed, which is exactly the NullRef in the stacktrace.
            if (!ValidateFlowEdgeBuffer(startNode) || !ValidateFlowEdgeBuffer(endNode))
                return true;

            return false;
        }

        /// <summary>
        ///     Attempts to iterate a node's <see cref="ConnectedFlowEdge"/> buffer and
        ///     verify every referenced edge entity carries a readable
        ///     <see cref="ElectricityFlowEdge"/> whose <c>m_Start</c>/<c>m_End</c>
        ///     still reference living entities.
        ///     Returns <c>false</c> when the buffer is unreadable, contains stale edge
        ///     references, or the flow edge data itself points to destroyed nodes.
        /// </summary>
        private bool ValidateFlowEdgeBuffer(Entity node) {
            try {
                var buffer = EntityManager.GetBuffer<ConnectedFlowEdge>(node, true);
                foreach (var connectedEdge in buffer) {
                    if (!EntityManager.Exists(connectedEdge.m_Edge) ||
                        !EntityManager.HasComponent<ElectricityFlowEdge>(connectedEdge.m_Edge))
                        return false;

                    // Mirrors flowEdges[entity] + the m_Start/m_End reads in TryGetFlowEdge
                    var flowEdge = EntityManager.GetComponentData<ElectricityFlowEdge>(connectedEdge.m_Edge);
                    if (!EntityManager.Exists(flowEdge.m_Start) ||
                        !EntityManager.Exists(flowEdge.m_End))
                        return false;
                }
                return true;
            } catch (Exception) {
                // Buffer exists on the archetype but the backing data is corrupted
                return false;
            }
        }
    }
}
