// <copyright file="NT_OverlayRenderSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using System.Diagnostics.CodeAnalysis;
    using Colossal.Mathematics;
    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Rendering;
    using Game.Tools;
    using NetworkTools.Components;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Utils;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Color = UnityEngine.Color;

    #endregion

    /// <summary>
    ///     Overlay Rendering System.
    /// </summary>
    public partial class NT_OverlayRenderSystem : GameSystemBase {
        private EntityQuery m_EdgeQuery;
        private EntityQuery m_TempEdgeQuery;
        private PrefixedLogger m_Log;
        private EntityQuery m_NodeQuery;
        private EntityQuery m_HandleQuery;
        private OverlayRenderSystem m_OverlayRenderSystem;
        private PreCullingSystem m_PreCullingSystem;
        private ToolSystem m_ToolSystem;

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(NT_OverlayRenderSystem));
            m_Log.Debug("OnCreate()");

            m_NodeQuery = SystemAPI.QueryBuilder()
                .WithAll<Node>()
                .WithAny<NT_Highlighted, NT_Selected, NT_Eligible, NT_SelectedFirst, NT_SelectedLast>()
                .WithNone<Deleted, Hidden>()
                .Build();

            m_EdgeQuery = SystemAPI.QueryBuilder()
                .WithAll<Edge>()
                .WithAny<NT_Highlighted, NT_Selected>()
                .WithNone<Deleted, Hidden>()
                .Build();


            m_TempEdgeQuery = SystemAPI.QueryBuilder()
                .WithAll<Edge, Temp>()
                .WithNone<Deleted, Hidden>()
                .Build();

            m_HandleQuery = SystemAPI.QueryBuilder()
                .WithAll<NT_Handle>()
                .WithNone<Deleted>()
                .Build();

            // Systems & References
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            m_PreCullingSystem    = World.GetOrCreateSystemManaged<PreCullingSystem>();
            m_ToolSystem          = World.GetOrCreateSystemManaged<ToolSystem>();
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            if (m_ToolSystem.activeTool is not NT_BaseToolSystem tool) {
                return;
            }

            var nodeBufferJobHandle = default(JobHandle);
            var markersBufferJobHandle = default(JobHandle);
            var edgeBufferJobHandle = default(JobHandle);
            var tempEdgeBufferJobHandle = default(JobHandle);
            var lastJobHandle = Dependency;

            if (tool.RenderEligibleNodes) {
                // Draw nodes
                var drawNodesJob = new DrawNodesJob {
                    m_Buffer                           = m_OverlayRenderSystem.GetBuffer(out nodeBufferJobHandle),
                    m_EntityTypeHandle                 = SystemAPI.GetEntityTypeHandle(),
                    m_HighlightedComponentTypeHandle   = SystemAPI.GetComponentTypeHandle<NT_Highlighted>(),
                    m_SelectedComponentTypeHandle      = SystemAPI.GetComponentTypeHandle<NT_Selected>(),
                    m_EligibleComponentTypeHandle      = SystemAPI.GetComponentTypeHandle<NT_Eligible>(),
                    m_SelectedFirstComponentTypeHandle = SystemAPI.GetComponentTypeHandle<NT_SelectedFirst>(),
                    m_SelectedLastComponentTypeHandle  = SystemAPI.GetComponentTypeHandle<NT_SelectedLast>(),
                    m_NodeComponentTypeHandle          = SystemAPI.GetComponentTypeHandle<Node>(),
                    m_NodeGeometryComponentTypeHandle  = SystemAPI.GetComponentTypeHandle<NodeGeometry>(),
                    m_ConnectedEdgeComponentTypeHandle = SystemAPI.GetBufferTypeHandle<ConnectedEdge>(true),
                    m_EdgeLookup                       = SystemAPI.GetComponentLookup<Edge>(true),
                    m_EdgeGeometryLookup               = SystemAPI.GetComponentLookup<EdgeGeometry>(true),
                    m_CurveLookup                      = SystemAPI.GetComponentLookup<Curve>(true)
                };

                var drawNodesJobHandle = drawNodesJob.ScheduleByRef(m_NodeQuery,
                    JobHandle.CombineDependencies(Dependency,
                        nodeBufferJobHandle));

                m_OverlayRenderSystem.AddBufferWriter(drawNodesJobHandle);
                lastJobHandle = drawNodesJobHandle;
            }

            if (tool.RenderHandles) {
                // Draw markers
                var drawHandlesJob = new DrawHandlesJob {
                    m_Buffer                              = m_OverlayRenderSystem.GetBuffer(out markersBufferJobHandle),
                    m_EntityTypeHandle                    = SystemAPI.GetEntityTypeHandle(),
                    m_NTHandlePositionComponentTypeHandle = SystemAPI.GetComponentTypeHandle<NT_HandlePosition>(),
                    m_HighlightedComponentTypeHandle      = SystemAPI.GetComponentTypeHandle<NT_Highlighted>(),
                    m_SelectedComponentTypeHandle         = SystemAPI.GetComponentTypeHandle<NT_Selected>(),
                    m_NTHandleComponentTypeHandle         = SystemAPI.GetComponentTypeHandle<NT_Handle>(),
                    m_NTHandleLinkComponentTypeHandle     = SystemAPI.GetComponentTypeHandle<NT_HandleLink>(),
                    m_NodeLookup                          = SystemAPI.GetComponentLookup<Node>(true),
                    m_CurveLookup                         = SystemAPI.GetComponentLookup<Curve>(true),
                };

                var drawHandlesJobHandle = drawHandlesJob.ScheduleByRef(m_HandleQuery,
                    JobHandle.CombineDependencies(Dependency,
                        markersBufferJobHandle));

                m_OverlayRenderSystem.AddBufferWriter(drawHandlesJobHandle);
                lastJobHandle = drawHandlesJobHandle;
            }

            if (tool.RenderEligibleEdges) {
                // Draw edges
                var drawEdgesJob = new DrawEdgesJob {
                    m_Buffer                               = m_OverlayRenderSystem.GetBuffer(out edgeBufferJobHandle),
                    m_HighlightedComponentTypeHandle       = SystemAPI.GetComponentTypeHandle<NT_Highlighted>(),
                    m_SelectedComponentTypeHandle          = SystemAPI.GetComponentTypeHandle<NT_Selected>(),
                    m_EdgeComponentTypeHandle              = SystemAPI.GetComponentTypeHandle<Edge>(),
                    m_CurveComponentTypeHandle             = SystemAPI.GetComponentTypeHandle<Curve>(),
                    m_EdgeGeometryComponentTypeHandle      = SystemAPI.GetComponentTypeHandle<EdgeGeometry>(),
                    m_StartNodeGeometryComponentTypeHandle = SystemAPI.GetComponentTypeHandle<StartNodeGeometry>(),
                    m_EndNodeGeometryComponentTypeHandle   = SystemAPI.GetComponentTypeHandle<EndNodeGeometry>(),
                    m_NodeLookup                           = SystemAPI.GetComponentLookup<Node>(true)
                };

                var drawEdgesJobHandle = drawEdgesJob.ScheduleByRef(m_EdgeQuery,
                    JobHandle.CombineDependencies(lastJobHandle,
                        edgeBufferJobHandle));

                m_OverlayRenderSystem.AddBufferWriter(drawEdgesJobHandle);
                lastJobHandle = drawEdgesJobHandle;
            }

            if (tool.RenderTempEdges) {
                // Draw edges
                var drawTempEdgesJob = new DrawTempEdgesJob {
                    m_Buffer                               = m_OverlayRenderSystem.GetBuffer(out tempEdgeBufferJobHandle),
                    m_HighlightedComponentTypeHandle       = SystemAPI.GetComponentTypeHandle<NT_Highlighted>(),
                    m_SelectedComponentTypeHandle          = SystemAPI.GetComponentTypeHandle<NT_Selected>(),
                    m_EdgeComponentTypeHandle              = SystemAPI.GetComponentTypeHandle<Edge>(),
                    m_CurveComponentTypeHandle             = SystemAPI.GetComponentTypeHandle<Curve>(),
                    m_EdgeGeometryComponentTypeHandle      = SystemAPI.GetComponentTypeHandle<EdgeGeometry>(),
                    m_StartNodeGeometryComponentTypeHandle = SystemAPI.GetComponentTypeHandle<StartNodeGeometry>(),
                    m_EndNodeGeometryComponentTypeHandle   = SystemAPI.GetComponentTypeHandle<EndNodeGeometry>(),
                    m_NodeLookup                           = SystemAPI.GetComponentLookup<Node>(true)
                };

                var drawTempEdgesJobHandle = drawTempEdgesJob.ScheduleByRef(m_TempEdgeQuery,
                    JobHandle.CombineDependencies(lastJobHandle,
                        tempEdgeBufferJobHandle));

                m_OverlayRenderSystem.AddBufferWriter(drawTempEdgesJobHandle);
                lastJobHandle = drawTempEdgesJobHandle;
            }

            Dependency = lastJobHandle;
        }
    }
}