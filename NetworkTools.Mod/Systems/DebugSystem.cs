namespace NetworkTools.Systems {
    using System.Collections.Generic;

    using Game;
    using Game.Net;
    using Game.Prefabs;
    using Game.Simulation;

    using LucaModsCommon.Utils;

    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    ///     Deserialize-time electricity flow-graph corruption checker. Runs once per save load,
    ///     immediately after the vanilla <c>ElectricityGraphSystem</c>, and logs every issue the
    ///     shared <see cref="ElectricityGraphVerifier"/> finds across all electricity edges.
    /// </summary>
    public partial class NT_DebugSystem : GameSystemBase {

        /// <summary>
        ///     Logger instance
        /// </summary>
        private PrefixedLogger m_Log;
        private EntityQuery m_EntitiesToProcessQuery;

        /// <summary>
        ///     Per-pass cache of electricity-node structural health, shared across edges so a node
        ///     reachable from multiple edges is validated (and logged) only once.
        /// </summary>
        private readonly Dictionary<Entity, bool> m_NodeHealth = new Dictionary<Entity, bool>();
        private readonly List<ElectricityIssue> m_Issues = new List<ElectricityIssue>();

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
            var counts = new int[5];

            foreach (var entity in entities)
            {
                m_Issues.Clear();
                ElectricityGraphVerifier.VerifyEdge(EntityManager, entity, m_NodeHealth, m_Issues);
                foreach (var issue in m_Issues)
                {
                    m_Log.Debug(issue.Message);
                    counts[(int)issue.Kind]++;
                }
            }

            var total = counts[(int)ElectricityIssueKind.InvalidNode]
                        + counts[(int)ElectricityIssueKind.MissingBuffer]
                        + counts[(int)ElectricityIssueKind.CorruptBuffer]
                        + counts[(int)ElectricityIssueKind.MissingNodeConnection]
                        + counts[(int)ElectricityIssueKind.MissingFlowEdge];

            m_Log.Debug(
                $"Verification complete across {entities.Length} edges. {total} issue(s): " +
                $"{counts[(int)ElectricityIssueKind.InvalidNode]} invalid/stale node(s), " +
                $"{counts[(int)ElectricityIssueKind.MissingBuffer]} missing buffer(s), " +
                $"{counts[(int)ElectricityIssueKind.CorruptBuffer]} corrupt buffer(s), " +
                $"{counts[(int)ElectricityIssueKind.MissingNodeConnection]} missing node connection(s), " +
                $"{counts[(int)ElectricityIssueKind.MissingFlowEdge]} missing flow edge(s).");

            entities.Dispose();
        }
    }
}
