namespace NetworkTools.Systems {
    using System.Collections.Generic;

    using Colossal.Entities;

    using Game;
    using Game.Common;
    using Game.Simulation;

    using LucaModsCommon.Utils;

    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    ///     Runtime watchdog for <em>orphaned electricity flow nodes</em> — the blind spot that the
    ///     edge-anchored <see cref="NT_ElectricityWatchdogSystem"/> and the game's deserialize-time
    ///     <c>ElectricityGraphSystem</c> both share.
    ///
    ///     Those two checkers start from net <c>Edge</c> entities and walk <em>outward</em> to flow
    ///     nodes, so a flow node whose owning net node/edge has been destroyed — leaving the flow node
    ///     (an <see cref="ElectricityFlowNode"/> reachable via <c>ElectricityNodeConnection.m_ElectricityNode</c>)
    ///     leaked with no net owner — is never enumerated by either.
    ///
    ///     That leak is exactly the residue left when a net node is deleted <em>after</em>
    ///     <c>ElectricityGraphDeleteSystem</c> (Modification1) has already run for the frame — e.g. our
    ///     own <c>NT_PostProcessingSystem</c> deletes selected super-nodes at Modification4. The node is
    ///     destroyed by <c>CleanUpSystem</c> the same frame, so <c>ElectricityGraphDeleteSystem</c> never
    ///     tears its flow node down, and the flow node is neither cleaned nor ever again referenced.
    ///
    ///     This system scans the <em>reverse</em> direction: it enumerates every live flow node and
    ///     asserts each is still claimed by a living owner — a net node/edge
    ///     (<see cref="ElectricityNodeConnection"/>), a valve (<see cref="ElectricityValveConnection"/>),
    ///     a building (<see cref="ElectricityBuildingConnection"/>: transformer plus its producer,
    ///     consumer, charge and discharge flow edges), or the flow system's source/sink node. Anything
    ///     unclaimed is an orphan.
    ///
    ///     Like the sibling watchdog it is throttled and debounced: a flow node is only reported once it
    ///     has been unclaimed across two consecutive scans, so the transient window during a legitimate
    ///     deletion — where the net owner is marked <c>Deleted</c> in the same frame its flow node is torn
    ///     down — never produces a false positive (a still-existing <c>Deleted</c> owner still counts).
    /// </summary>
    public partial class NT_ElectricityOrphanWatchdogSystem : GameSystemBase {

        private PrefixedLogger m_Log;
        private ElectricityFlowSystem m_ElectricityFlowSystem;

        private EntityQuery m_FlowNodeQuery;
        private EntityQuery m_NodeConnectionQuery;
        private EntityQuery m_ValveConnectionQuery;
        private EntityQuery m_BuildingConnectionQuery;

        // Flow nodes claimed by a living owner; rebuilt from scratch every scan.
        private readonly HashSet<Entity> m_OwnedFlowNodes = new HashSet<Entity>();

        // Debounce state: flow nodes orphaned in the previous / current scan, and orphans already
        // reported (so one continuous leak is logged once, but a later recurrence is logged afresh).
        private HashSet<Entity> m_OrphanPrev = new HashSet<Entity>();
        private HashSet<Entity> m_OrphanCurr = new HashSet<Entity>();
        private readonly HashSet<Entity> m_Reported = new HashSet<Entity>();

        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(NT_ElectricityOrphanWatchdogSystem));
            m_ElectricityFlowSystem = World.GetOrCreateSystemManaged<ElectricityFlowSystem>();

            // Every live flow node — mirrors ElectricityFlowSystem's own node group.
            m_FlowNodeQuery = SystemAPI.QueryBuilder()
                                       .WithAll<ElectricityFlowNode, ConnectedFlowEdge>()
                                       .WithNone<Deleted>()
                                       .Build();

            // Owner components. No Temp/Deleted filter on purpose: any *existing* owner — even one
            // mid-deletion — still accounts for its flow node, which is what keeps legitimate deletions
            // from being flagged during the frame the owner and its flow node are both torn down.
            m_NodeConnectionQuery     = SystemAPI.QueryBuilder().WithAll<ElectricityNodeConnection>().Build();
            m_ValveConnectionQuery    = SystemAPI.QueryBuilder().WithAll<ElectricityValveConnection>().Build();
            m_BuildingConnectionQuery = SystemAPI.QueryBuilder().WithAll<ElectricityBuildingConnection>().Build();

            RequireForUpdate(m_FlowNodeQuery);
        }

        /// <summary>
        ///     Throttle the scan — it walks every flow node and every owner on the main thread, so there
        ///     is no need to run it each simulation frame. Lower this for snappier detection while hunting.
        /// </summary>
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 64;

        /// <inheritdoc />
        protected override void OnUpdate() {
            BuildOwnedSet();

            var flowNodes = m_FlowNodeQuery.ToEntityArray(Allocator.Temp);
            m_OrphanCurr.Clear();
            var newlyConfirmed = 0;

            foreach (var flowNode in flowNodes) {
                if (m_OwnedFlowNodes.Contains(flowNode))
                    continue;

                m_OrphanCurr.Add(flowNode);

                // Only report once the orphan has survived a full scan interval (i.e. it was also
                // unclaimed last scan) and has not already been reported this episode.
                if (m_OrphanPrev.Contains(flowNode) && m_Reported.Add(flowNode)) {
                    var edgeCount = EntityManager.TryGetBuffer<ConnectedFlowEdge>(flowNode, true, out var buffer)
                        ? buffer.Length
                        : -1;
                    m_Log.Error(
                        $"Orphaned electricity flow node {flowNode.Index}:{flowNode.Version} — no living net " +
                        $"node/edge, valve, building or source/sink owner (ConnectedFlowEdge count: {edgeCount}). " +
                        "Its owning net entity was destroyed without ElectricityGraphDeleteSystem tearing the " +
                        "flow node down — this is the leak the edge-anchored checkers cannot see.");
                    newlyConfirmed++;
                }
            }

            // Forget flow nodes that were re-claimed or destroyed, so a later recurrence is reported afresh.
            m_Reported.IntersectWith(m_OrphanCurr);

            // Current scan becomes the baseline for the next one (reuse the old set as scratch).
            var swap = m_OrphanPrev;
            m_OrphanPrev = m_OrphanCurr;
            m_OrphanCurr = swap;

            flowNodes.Dispose();

            if (newlyConfirmed > 0)
                m_Log.Error($"Electricity orphan watchdog: {newlyConfirmed} newly-confirmed orphaned flow node(s) " +
                            $"this scan; {m_Reported.Count} currently orphaned.");
        }

        /// <summary>
        ///     Rebuilds <see cref="m_OwnedFlowNodes"/> with every flow node claimed by a living owner:
        ///     net nodes/edges (<see cref="ElectricityNodeConnection"/>), valves, buildings (transformer
        ///     plus the four edge-derived nodes) and the flow system's source/sink nodes. Over-counting
        ///     an owner is harmless (it merely hides a would-be orphan); under-counting would falsely flag
        ///     a live flow node, so the collection errs toward inclusion.
        /// </summary>
        private void BuildOwnedSet() {
            m_OwnedFlowNodes.Clear();

            // Source & sink are global flow nodes with no net owner — never orphans.
            m_OwnedFlowNodes.Add(m_ElectricityFlowSystem.sourceNode);
            m_OwnedFlowNodes.Add(m_ElectricityFlowSystem.sinkNode);

            // Net nodes AND net edges both carry ElectricityNodeConnection.
            var nodeConnections = m_NodeConnectionQuery.ToComponentDataArray<ElectricityNodeConnection>(Allocator.Temp);
            foreach (var connection in nodeConnections)
                m_OwnedFlowNodes.Add(connection.m_ElectricityNode);
            nodeConnections.Dispose();

            var valveConnections = m_ValveConnectionQuery.ToComponentDataArray<ElectricityValveConnection>(Allocator.Temp);
            foreach (var connection in valveConnections)
                m_OwnedFlowNodes.Add(connection.m_ValveNode);
            valveConnections.Dispose();

            var buildingConnections = m_BuildingConnectionQuery.ToComponentDataArray<ElectricityBuildingConnection>(Allocator.Temp);
            foreach (var connection in buildingConnections) {
                m_OwnedFlowNodes.Add(connection.m_TransformerNode);
                // Producer/consumer/charge/discharge are flow *edges*; both their endpoints are flow nodes
                // owned by this building. Adding both endpoints avoids depending on the producer/consumer
                // accessor conventions (over-inclusion is the safe direction here).
                AddFlowEdgeEndpoints(connection.m_ProducerEdge);
                AddFlowEdgeEndpoints(connection.m_ConsumerEdge);
                AddFlowEdgeEndpoints(connection.m_ChargeEdge);
                AddFlowEdgeEndpoints(connection.m_DischargeEdge);
            }
            buildingConnections.Dispose();
        }

        /// <summary>
        ///     Adds both endpoints of a building flow edge to the owned set, if the edge is set and still
        ///     carries a readable <see cref="ElectricityFlowEdge"/>.
        /// </summary>
        private void AddFlowEdgeEndpoints(Entity flowEdge) {
            if (flowEdge == Entity.Null || !EntityManager.TryGetComponent<ElectricityFlowEdge>(flowEdge, out var edge))
                return;
            m_OwnedFlowNodes.Add(edge.m_Start);
            m_OwnedFlowNodes.Add(edge.m_End);
        }
    }
}
