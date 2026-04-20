namespace NetworkTools.Systems {
    using System;
    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Rendering;
    using Game.Tools;
    using NetworkTools.Components;
    using NetworkTools.Systems.Rendering;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using FrustumPlanes = Game.Rendering.FrustumPlanes;

    /// <summary>
    ///     Tool-specific overlay rendering system.
    /// </summary>
    public partial class NT_ToolOverlayRenderSystem : GameSystemBase {
        /// <summary>Maximum render distance for overlay entities (squared).</summary>
        private const float MAX_OVERLAY_DISTANCE    = 3000f * 3000f;

        private PrefixedLogger            m_Log;
        private CustomOverlayRenderSystem m_OverlayRenderSystem;
        private ToolSystem                m_ToolSystem;

        // AddNode query: edges that are either NT-tagged or Temp
        private EntityQuery m_AddNodeQuery;

        // Narrow query for temp edges only, used by CollectTempOriginalsJob
        private EntityQuery m_TempEdgeQuery;

        // Shared temp originals set, available to all tool overlay methods
        private NativeParallelHashSet<Entity> m_TempOriginals;

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(NT_ToolOverlayRenderSystem));
            m_Log.Debug("OnCreate()");

            m_AddNodeQuery = SystemAPI.QueryBuilder()
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

            // Exit early on empty queries
            var activeQuery = GetQueryForTool(tool);

            if (!activeQuery.HasValue || (activeQuery.Value.IsEmptyIgnoreFilter && m_TempEdgeQuery.IsEmptyIgnoreFilter)) {
                return;
            }

            // 1. Collect temp entities so that we can use them in parallel jobs
            m_TempOriginals = new NativeParallelHashSet<Entity>(16, Allocator.TempJob);

            var collectJob = new CollectTempOriginalsJob {
                m_TempComponentTypeHandle = SystemAPI.GetComponentTypeHandle<Temp>(),
                m_TempOriginals           = m_TempOriginals.AsParallelWriter()
            };

            var collectHandle = collectJob.ScheduleByRef(m_TempEdgeQuery, Dependency);
            var overlayBuffer = m_OverlayRenderSystem.GetBuffer(out var bufferDeps);

            // 2. Shared frustum + distance culling pre-pass
            var cameraPos      = Camera.main != null ? (float3)Camera.main.transform.position : float3.zero;
            var visibleEntities = new NativeParallelHashSet<Entity>(128, Allocator.TempJob);
            var planePackets = BuildCullingPlanes();

            var cullJob = new FrustumCullEntitiesJob {
                m_EntityTypeHandle         = SystemAPI.GetEntityTypeHandle(),
                m_CurveComponentTypeHandle = SystemAPI.GetComponentTypeHandle<Curve>(),
                m_NodeComponentTypeHandle  = SystemAPI.GetComponentTypeHandle<Node>(),
                m_CullingPlanes            = planePackets.AsArray(),
                m_CameraPosition           = cameraPos,
                m_MaxDistance              = MAX_OVERLAY_DISTANCE,
                m_VisibleEntities          = visibleEntities.AsParallelWriter(),
            };

            var cullHandle = cullJob.ScheduleParallel(activeQuery.Value, collectHandle);

            // 3. Tool-specific prepare job
            var prepareHandle = SchedulePrepareJob(tool, cullHandle, visibleEntities, out var commandStream, out var chunkCount);

            // 4. Sequential dispatch of pre-computed commands to the overlay buffer
            var renderJob = new RenderOverlayCommandsJob {
                m_Buffer        = overlayBuffer,
                m_CommandReader = commandStream.AsReader(),
                m_ForEachCount  = chunkCount,
            };

            var renderHandle = renderJob.Schedule(JobHandle.CombineDependencies(prepareHandle, bufferDeps));

            m_OverlayRenderSystem.AddBufferWriter(renderHandle);
            m_TempOriginals.Dispose(renderHandle);
            visibleEntities.Dispose(renderHandle);
            commandStream.Dispose(renderHandle);
            planePackets.Dispose(renderHandle);
            Dependency = renderHandle;
        }

        /// <summary>
        ///     Builds SOA frustum plane packets from the main camera for per-entity culling.
        /// </summary>
        private static NativeList<FrustumPlanes.PlanePacket4> BuildCullingPlanes() {
            var planePackets = new NativeList<FrustumPlanes.PlanePacket4>(2, Allocator.TempJob);

            if (Camera.main is null) {
                return planePackets;
            }

            var managedPlanes = GeometryUtility.CalculateFrustumPlanes(Camera.main);
            var nativePlanes  = new NativeArray<Plane>(6, Allocator.TempJob);
            nativePlanes.CopyFrom(managedPlanes);
            FrustumPlanes.BuildSOAPlanePackets(nativePlanes, 6, planePackets);
            nativePlanes.Dispose();

            return planePackets;
        }

        /// <summary>
        ///     Returns the entity query for the given tool, or null if unrecognised.
        /// </summary>
        private EntityQuery? GetQueryForTool(NT_BaseToolSystem tool) {
            return tool switch {
                //NT_AddNodeToolSystem => m_AddNodeQuery,
                _ => null
            };
        }

        /// <summary>
        ///     Dispatches to the correct tool-specific prepare job.
        /// </summary>
        private JobHandle SchedulePrepareJob(
            NT_BaseToolSystem tool,
            JobHandle inputDeps,
            NativeParallelHashSet<Entity> visibleEntities,
            out NativeStream commandStream,
            out int chunkCount) {
            return tool switch {
                //NT_AddNodeToolSystem => ScheduleAddNodePrepare(inputDeps, visibleEntities, out commandStream, out chunkCount),
                _ => throw new NotImplementedException($"No prepare job implemented for tool type: {tool.GetType().Name}")
            };
        }

        /// <summary>
        ///     Schedules the parallel prepare job for the AddNode tool overlay.
        /// </summary>
        private JobHandle ScheduleAddNodePrepare(
            JobHandle inputDeps,
            NativeParallelHashSet<Entity> visibleEntities,
            out NativeStream commandStream,
            out int chunkCount) {
            chunkCount    = math.max(1, m_AddNodeQuery.CalculateChunkCountWithoutFiltering());
            commandStream = new NativeStream(chunkCount, Allocator.TempJob);

            var prepareJob = new PrepareAddNodeCommandsJob {
                m_Colors                         = RenderColors.Default,
                m_EntityTypeHandle               = SystemAPI.GetEntityTypeHandle(),
                m_EdgeComponentTypeHandle        = SystemAPI.GetComponentTypeHandle<Edge>(),
                m_CurveComponentTypeHandle       = SystemAPI.GetComponentTypeHandle<Curve>(),
                m_EligibleComponentTypeHandle    = SystemAPI.GetComponentTypeHandle<NT_Eligible>(),
                m_HighlightedComponentTypeHandle = SystemAPI.GetComponentTypeHandle<NT_Highlighted>(),
                m_SelectedComponentTypeHandle    = SystemAPI.GetComponentTypeHandle<NT_Selected>(),
                m_TempComponentTypeHandle        = SystemAPI.GetComponentTypeHandle<Temp>(),
                m_TempOriginals                  = m_TempOriginals,
                m_VisibleEntities                = visibleEntities,
                m_NodeLookup                     = SystemAPI.GetComponentLookup<Node>(true),
                m_TempLookup                     = SystemAPI.GetComponentLookup<Temp>(true),
                m_EdgeLookup                     = SystemAPI.GetComponentLookup<Edge>(true),
                m_CommandWriter                  = commandStream.AsWriter(),
            };

            return prepareJob.ScheduleParallel(m_AddNodeQuery, inputDeps);
        }
    }
}
