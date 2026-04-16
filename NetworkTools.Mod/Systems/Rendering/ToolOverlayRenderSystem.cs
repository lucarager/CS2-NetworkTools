namespace NetworkTools.Systems {
    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Tools;
    using NetworkTools.Components;
    using NetworkTools.Systems.Rendering;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    /// <summary>
    ///     Tool-specific overlay rendering system.
    ///     Each tool gets its own render job scheduled via a switch on the active tool type.
    /// </summary>
    public partial class NT_ToolOverlayRenderSystem : GameSystemBase {
        private PrefixedLogger            m_Log;
        private CustomOverlayRenderSystem m_OverlayRenderSystem;
        private ToolSystem                m_ToolSystem;

        // AddNode query: edges that are either NT-tagged or Temp
        private EntityQuery m_AddNodeEdgeQuery;

        // Narrow query for temp edges only, used by CollectTempOriginalsJob
        private EntityQuery m_TempEdgeQuery;

        // Shared temp originals set, available to all tool overlay methods
        private NativeParallelHashSet<Entity> m_TempOriginals;

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(NT_ToolOverlayRenderSystem));
            m_Log.Debug("OnCreate()");

            m_AddNodeEdgeQuery = SystemAPI.QueryBuilder()
                                          .WithAll<Edge, Curve, EdgeGeometry>()
                                          .WithAny<NT_Eligible, NT_Highlighted, NT_Selected, Temp>()
                                          .WithNone<Deleted, Hidden>()
                                          .Build();

            m_TempEdgeQuery = SystemAPI.QueryBuilder()
                                       .WithAll<Edge, Temp>()
                                       .WithNone<Deleted, Hidden>()
                                       .Build();

            // Systems
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<CustomOverlayRenderSystem>();
            m_ToolSystem          = World.GetOrCreateSystemManaged<ToolSystem>();
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            if (m_ToolSystem.activeTool is not NT_BaseToolSystem tool) {
                return;
            }

            // Collect temp originals once, shared by all tool overlay methods
            m_TempOriginals = new NativeParallelHashSet<Entity>(16, Allocator.TempJob);

            var collectJob = new CollectTempOriginalsJob {
                m_TempComponentTypeHandle = SystemAPI.GetComponentTypeHandle<Temp>(),
                m_TempOriginals           = m_TempOriginals.AsParallelWriter()
            };

            var collectHandle = collectJob.ScheduleByRef(m_TempEdgeQuery, Dependency);

            switch (tool) {
                case NT_AddNodeToolSystem:
                    ScheduleAddNodeOverlay(collectHandle);
                    break;
            }

            // Dispose the set after all scheduled work completes
            m_TempOriginals.Dispose(Dependency);
        }

        /// <summary>
        ///     Schedules the combined overlay render job for the AddNode tool.
        /// </summary>
        private void ScheduleAddNodeOverlay(JobHandle collectHandle) {
            var drawAddNodeJob = new DrawAddNodeJob {
                m_Buffer                          = m_OverlayRenderSystem.GetBuffer(out var bufferJobHandle),
                m_Colors                          = RenderColors.Default,
                m_Dimensions                      = RenderDimensions.Default,
                m_EntityTypeHandle                = SystemAPI.GetEntityTypeHandle(),
                m_EdgeComponentTypeHandle         = SystemAPI.GetComponentTypeHandle<Edge>(),
                m_CurveComponentTypeHandle        = SystemAPI.GetComponentTypeHandle<Curve>(),
                m_EdgeGeometryComponentTypeHandle = SystemAPI.GetComponentTypeHandle<EdgeGeometry>(),
                m_EligibleComponentTypeHandle     = SystemAPI.GetComponentTypeHandle<NT_Eligible>(),
                m_HighlightedComponentTypeHandle  = SystemAPI.GetComponentTypeHandle<NT_Highlighted>(),
                m_SelectedComponentTypeHandle     = SystemAPI.GetComponentTypeHandle<NT_Selected>(),
                m_TempComponentTypeHandle         = SystemAPI.GetComponentTypeHandle<Temp>(),
                m_TempOriginals                   = m_TempOriginals,
                m_NodeLookup                      = SystemAPI.GetComponentLookup<Node>(true),
                m_EdgeGeometryLookup              = SystemAPI.GetComponentLookup<EdgeGeometry>(true),
                m_TempLookup                      = SystemAPI.GetComponentLookup<Temp>(true),
                m_EdgeLookup                      = SystemAPI.GetComponentLookup<Edge>(true)
            };

            var drawHandle = drawAddNodeJob.ScheduleByRef(m_AddNodeEdgeQuery,
                                                           JobHandle.CombineDependencies(collectHandle,
                                                                                         bufferJobHandle));

            m_OverlayRenderSystem.AddBufferWriter(drawHandle);
            Dependency = drawHandle;
        }
    }
}
