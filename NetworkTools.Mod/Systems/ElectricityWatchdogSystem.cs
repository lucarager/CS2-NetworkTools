namespace NetworkTools.Systems {
    using System.Collections.Generic;

    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;

    using LucaModsCommon.Utils;

    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    ///     Runtime watchdog that scans the live electricity flow graph for the same corruption the
    ///     game reports on load (missing flow edges, broken <c>ConnectedFlowEdge</c> buffers), so it
    ///     can be caught during play — before it is written into a save.
    ///
    ///     The flow graph is rebuilt asynchronously over several frames after any network edit
    ///     (<c>ElectricityEdgeGraphSystem</c> only acts on freshly <c>Created</c> edges via
    ///     <c>ModificationBarrier2B</c>), so a single scan can momentarily see a half-built graph.
    ///     To avoid false alarms this runs throttled and only reports corruption that has *persisted*
    ///     across two consecutive scans — a transient mid-edit state never survives that long, but the
    ///     corruption that would be serialized does.
    /// </summary>
    public partial class NT_ElectricityWatchdogSystem : GameSystemBase {

        private PrefixedLogger m_Log;
        private EntityQuery m_EdgeQuery;

        private readonly Dictionary<Entity, bool> m_NodeHealth = new Dictionary<Entity, bool>();
        private readonly List<ElectricityIssue> m_Issues = new List<ElectricityIssue>();

        // Debounce state: edges corrupt in the previous / current scan, and edges already reported
        // (so a continuous corruption episode is logged once, but a recurrence is logged again).
        private HashSet<Entity> m_CorruptPrev = new HashSet<Entity>();
        private HashSet<Entity> m_CorruptCurr = new HashSet<Entity>();
        private readonly HashSet<Entity> m_Reported = new HashSet<Entity>();

        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(NT_ElectricityWatchdogSystem));

            // Settled, real electricity edges only — Temp are tool previews (incl. ours, which never
            // get a flow graph) and Deleted are on their way out; both would be false positives.
            m_EdgeQuery = SystemAPI.QueryBuilder()
                                   .WithAll<Game.Net.ElectricityConnection, ElectricityNodeConnection, Edge, PrefabRef>()
                                   .WithNone<Temp, Deleted>()
                                   .Build();
            RequireForUpdate(m_EdgeQuery);
        }

        /// <summary>
        ///     Throttle the scan — it walks every electricity edge on the main thread, so there is no
        ///     need to run it every simulation frame. Lower this for snappier detection while hunting.
        /// </summary>
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 64;

        /// <inheritdoc />
        protected override void OnUpdate() {
            var entities = m_EdgeQuery.ToEntityArray(Allocator.Temp);

            m_NodeHealth.Clear();
            m_CorruptCurr.Clear();
            var newlyConfirmed = 0;

            foreach (var entity in entities)
            {
                m_Issues.Clear();
                ElectricityGraphVerifier.VerifyEdge(EntityManager, entity, m_NodeHealth, m_Issues);

                if (m_Issues.Count == 0)
                    continue;

                m_CorruptCurr.Add(entity);

                // Only report once the corruption has survived a full scan interval (i.e. it was also
                // corrupt last scan) and has not already been reported this episode.
                if (m_CorruptPrev.Contains(entity) && m_Reported.Add(entity))
                {
                    m_Log.Error($"Persistent electricity corruption on net edge {entity.Index} ({m_Issues.Count} issue(s)):");
                    foreach (var issue in m_Issues)
                        m_Log.Error($"  [{issue.Kind}] {issue.Message}");
                    newlyConfirmed++;
                }
            }

            // Forget edges that recovered or were removed, so a later recurrence is reported afresh.
            m_Reported.IntersectWith(m_CorruptCurr);

            // Current scan becomes the baseline for the next one (reuse the old set as scratch).
            var swap = m_CorruptPrev;
            m_CorruptPrev = m_CorruptCurr;
            m_CorruptCurr = swap;

            entities.Dispose();

            if (newlyConfirmed > 0)
                m_Log.Error($"Electricity watchdog: {newlyConfirmed} newly-confirmed corrupt edge(s) this scan; {m_Reported.Count} currently corrupt.");
        }
    }
}
