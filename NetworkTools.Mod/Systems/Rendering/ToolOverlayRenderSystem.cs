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
    using NetworkTools.Systems.Tools.Connect;
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

        // RemoveNode query: eligible nodes and temp-deleted nodes
        private EntityQuery m_RemoveNodeQuery;

        // Stateful nodes query: eligible/highlighted/selected nodes (shared across tools)
        private EntityQuery m_StatefulNodeQuery;

        // Narrow query for temp edges only, used by CollectTempOriginalsJob
        private EntityQuery m_TempEdgeQuery;

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

            m_RemoveNodeQuery = SystemAPI.QueryBuilder()
                                        .WithAll<Node>()
                                        .WithAny<NT_Eligible, Temp>()
                                        .WithNone<Deleted, Hidden>()
                                        .Build();

            m_StatefulNodeQuery = SystemAPI.QueryBuilder()
                                         .WithAll<Node, ConnectedEdge>()
                                         .WithAny<NT_Eligible, NT_Highlighted, NT_Selected,
                                             NT_SelectedFirst, NT_SelectedLast>()
                                         .WithNone<Temp, Deleted>()
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

            if (!activeQuery.HasValue || activeQuery.Value.IsEmptyIgnoreFilter) {
                return;
            }

            // 1. Collect temp originals so the render job can suppress them
            var tempOriginals = new NativeParallelHashSet<Entity>(m_TempEdgeQuery.CalculateEntityCountWithoutFiltering(), Allocator.TempJob);

            var collectJob = new CollectTempOriginalsJob {
                m_TempComponentTypeHandle = SystemAPI.GetComponentTypeHandle<Temp>(),
                m_TempOriginals           = tempOriginals.AsParallelWriter()
            };

            var collectHandle = collectJob.ScheduleByRef(m_TempEdgeQuery, Dependency);
            var overlayBuffer = m_OverlayRenderSystem.GetBuffer(out var bufferDeps);

            // 2. Shared frustum + distance culling pre-pass
            var cameraPos      = Camera.main != null ? (float3)Camera.main.transform.position : float3.zero;
            var visibleEntities = new NativeParallelHashSet<Entity>(activeQuery.Value.CalculateEntityCountWithoutFiltering(), Allocator.TempJob);
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

            // 3. Tool-specific render job (sequential — writes directly to the overlay buffer)
            var renderDeps   = JobHandle.CombineDependencies(cullHandle, bufferDeps);
            var renderHandle = ScheduleRenderJob(tool, renderDeps, tempOriginals, visibleEntities, overlayBuffer);

            m_OverlayRenderSystem.AddBufferWriter(renderHandle);
            tempOriginals.Dispose(renderHandle);
            visibleEntities.Dispose(renderHandle);
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
                NT_AddNodeToolSystem        => m_AddNodeQuery,
                NT_RemoveNodeToolSystem     => m_RemoveNodeQuery,
                NT_SuperNodeToolSystem      => m_StatefulNodeQuery,
                NT_ConnectToolSystem        => m_StatefulNodeQuery,
                NT_PathSelectionToolSystem  => m_StatefulNodeQuery,
                _ => null
            };
        }

        /// <summary>
        ///     Dispatches to the correct tool-specific render job.
        /// </summary>
        private JobHandle ScheduleRenderJob(
            NT_BaseToolSystem tool,
            JobHandle inputDeps,
            NativeParallelHashSet<Entity> tempOriginals,
            NativeParallelHashSet<Entity> visibleEntities,
            CustomOverlayRenderSystem.Buffer overlayBuffer) {
            return tool switch {
                NT_AddNodeToolSystem       => ScheduleAddNodeRender(inputDeps, tempOriginals, visibleEntities, overlayBuffer),
                NT_RemoveNodeToolSystem    => ScheduleRemoveNodeRender(inputDeps, visibleEntities, overlayBuffer),
                NT_SuperNodeToolSystem     => ScheduleStatefulNodesRender(inputDeps, visibleEntities, overlayBuffer),
                NT_ConnectToolSystem       => ScheduleStatefulNodesRender(inputDeps, visibleEntities, overlayBuffer),
                NT_PathSelectionToolSystem => ScheduleStatefulNodesRender(inputDeps, visibleEntities, overlayBuffer),
                _ => throw new NotImplementedException($"No render job implemented for tool type: {tool.GetType().Name}")
            };
        }

        /// <summary>
        ///     Schedules the sequential render job for the AddNode tool overlay.
        /// </summary>
        private JobHandle ScheduleAddNodeRender(
            JobHandle inputDeps,
            NativeParallelHashSet<Entity> tempOriginals,
            NativeParallelHashSet<Entity> visibleEntities,
            CustomOverlayRenderSystem.Buffer overlayBuffer) {
            var renderJob = new RenderAddNodeOverlayJob {
                m_EntityTypeHandle               = SystemAPI.GetEntityTypeHandle(),
                m_EdgeComponentTypeHandle        = SystemAPI.GetComponentTypeHandle<Edge>(),
                m_CurveComponentTypeHandle       = SystemAPI.GetComponentTypeHandle<Curve>(),
                m_EligibleComponentTypeHandle    = SystemAPI.GetComponentTypeHandle<NT_Eligible>(),
                m_HighlightedComponentTypeHandle = SystemAPI.GetComponentTypeHandle<NT_Highlighted>(),
                m_SelectedComponentTypeHandle    = SystemAPI.GetComponentTypeHandle<NT_Selected>(),
                m_TempComponentTypeHandle        = SystemAPI.GetComponentTypeHandle<Temp>(),
                m_TempOriginals                  = tempOriginals,
                m_VisibleEntities                = visibleEntities,
                m_NodeLookup                     = SystemAPI.GetComponentLookup<Node>(true),
                m_TempLookup                     = SystemAPI.GetComponentLookup<Temp>(true),
                m_EdgeLookup                     = SystemAPI.GetComponentLookup<Edge>(true),
                m_EligibleLookup                 = SystemAPI.GetComponentLookup<NT_Eligible>(true),
                m_Buffer                         = overlayBuffer,
            };

            return renderJob.Schedule(m_AddNodeQuery, inputDeps);
        }

        /// <summary>
        ///     Schedules the sequential render job for the RemoveNode tool overlay.
        /// </summary>
        private JobHandle ScheduleRemoveNodeRender(
            JobHandle inputDeps,
            NativeParallelHashSet<Entity> visibleEntities,
            CustomOverlayRenderSystem.Buffer overlayBuffer) {
            var renderJob = new RenderRemoveNodeOverlayJob {
                m_EntityTypeHandle        = SystemAPI.GetEntityTypeHandle(),
                m_NodeComponentTypeHandle = SystemAPI.GetComponentTypeHandle<Node>(),
                m_TempComponentTypeHandle = SystemAPI.GetComponentTypeHandle<Temp>(),
                m_VisibleEntities         = visibleEntities,
                m_Buffer                  = overlayBuffer,
            };

            return renderJob.Schedule(m_RemoveNodeQuery, inputDeps);
        }

        /// <summary>
        ///     Schedules the shared stateful nodes render job used by multiple tools.
        /// </summary>
        private JobHandle ScheduleStatefulNodesRender(
            JobHandle inputDeps,
            NativeParallelHashSet<Entity> visibleEntities,
            CustomOverlayRenderSystem.Buffer overlayBuffer) {
            var renderJob = new RenderStatefulNodesOverlayJob {
                m_EntityTypeHandle               = SystemAPI.GetEntityTypeHandle(),
                m_NodeComponentTypeHandle        = SystemAPI.GetComponentTypeHandle<Node>(),
                m_EligibleComponentTypeHandle    = SystemAPI.GetComponentTypeHandle<NT_Eligible>(),
                m_HighlightedComponentTypeHandle = SystemAPI.GetComponentTypeHandle<NT_Highlighted>(),
                m_SelectedComponentTypeHandle      = SystemAPI.GetComponentTypeHandle<NT_Selected>(),
                m_SelectedFirstComponentTypeHandle = SystemAPI.GetComponentTypeHandle<NT_SelectedFirst>(),
                m_SelectedLastComponentTypeHandle  = SystemAPI.GetComponentTypeHandle<NT_SelectedLast>(),
                m_ConnectedEdgeBufferTypeHandle  = SystemAPI.GetBufferTypeHandle<ConnectedEdge>(true),
                m_EdgeLookup                     = SystemAPI.GetComponentLookup<Edge>(true),
                m_EdgeGeometryLookup             = SystemAPI.GetComponentLookup<EdgeGeometry>(true),
                m_StartNodeGeometryLookup        = SystemAPI.GetComponentLookup<StartNodeGeometry>(true),
                m_EndNodeGeometryLookup          = SystemAPI.GetComponentLookup<EndNodeGeometry>(true),
                m_VisibleEntities                = visibleEntities,
                m_Buffer                         = overlayBuffer,
            };

            return renderJob.Schedule(m_StatefulNodeQuery, inputDeps);
        }
    }
}
