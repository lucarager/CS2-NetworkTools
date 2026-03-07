// <copyright file="NT_OverlaySystem.cs" company="Luca Rager">
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
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Rendering;
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
    public partial class NT_OverlaySystem : GameSystemBase {
        private EntityQuery m_EdgeQuery;
        private EntityQuery m_TempEdgeQuery;
        private EntityQuery m_TempNodeQuery;
        private PrefixedLogger m_Log;
        private EntityQuery m_NodeQuery;
        private EntityQuery m_HandleQuery;
        private CustomOverlayRenderSystem m_OverlayRenderSystem;
        private ToolSystem m_ToolSystem;

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(NT_OverlaySystem));
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

            m_TempNodeQuery = SystemAPI.QueryBuilder()
                .WithAll<Node, Temp>()
                .WithNone<Deleted, Hidden>()
                .Build();

            m_HandleQuery = SystemAPI.QueryBuilder()
                .WithAll<NT_Handle>()
                .WithNone<Deleted>()
                .Build();

            // Systems & References
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<CustomOverlayRenderSystem>();
            m_ToolSystem          = World.GetOrCreateSystemManaged<ToolSystem>();
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            if (m_ToolSystem.activeTool is not NT_BaseToolSystem tool) {
                return;
            }

            if (tool.RenderEligibleNodes) {
                var drawNodesJob = new DrawNodesJob {
                    m_Buffer                           = m_OverlayRenderSystem.GetBuffer(out var nodeBufferJobHandle),
                    m_Colors                           = RenderColors.Default,
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
                Dependency = drawNodesJobHandle;
            }

            if (tool.RenderHandles) {
                var drawHandlesJob = new DrawHandlesJob {
                    m_Buffer                              = m_OverlayRenderSystem.GetBuffer(out var markersBufferJobHandle),
                    m_Colors                              = RenderColors.Default,
                    m_EntityTypeHandle                    = SystemAPI.GetEntityTypeHandle(),
                    m_NTHandlePositionComponentTypeHandle = SystemAPI.GetComponentTypeHandle<NT_HandlePosition>(),
                    m_HighlightedComponentTypeHandle      = SystemAPI.GetComponentTypeHandle<NT_Highlighted>(),
                    m_SelectedComponentTypeHandle         = SystemAPI.GetComponentTypeHandle<NT_Selected>(),
                    m_NTHandleComponentTypeHandle         = SystemAPI.GetComponentTypeHandle<NT_Handle>(),
                    m_NTHandleLinkComponentTypeHandle     = SystemAPI.GetComponentTypeHandle<NT_HandleLink>(),
                    m_HandleLineComponentTypeHandle       = SystemAPI.GetComponentTypeHandle<NT_HandleLine>(),
                    m_HandleCircleComponentTypeHandle     = SystemAPI.GetComponentTypeHandle<NT_HandleCircle>(),
                    m_HandleConstraintsComponentTypeHandle = SystemAPI.GetComponentTypeHandle<NT_HandleConstraints>(),
                    m_HandleParentComponentTypeHandle     = SystemAPI.GetComponentTypeHandle<NT_HandleParent>(),
                    m_HandlePositionLookup                = SystemAPI.GetComponentLookup<NT_HandlePosition>(true),
                    m_NodeLookup                          = SystemAPI.GetComponentLookup<Node>(true),
                    m_CurveLookup                         = SystemAPI.GetComponentLookup<Curve>(true),
                };

                var drawHandlesJobHandle = drawHandlesJob.ScheduleByRef(m_HandleQuery,
                    JobHandle.CombineDependencies(Dependency,
                        markersBufferJobHandle));

                m_OverlayRenderSystem.AddBufferWriter(drawHandlesJobHandle);
                Dependency = drawHandlesJobHandle;
            }

            if (tool.RenderEligibleEdges) {
                var drawEdgesJob = new DrawEdgesJob {
                    m_Buffer                               = m_OverlayRenderSystem.GetBuffer(out var edgeBufferJobHandle),
                    m_Colors                               = RenderColors.Default,
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
                    JobHandle.CombineDependencies(Dependency,
                        edgeBufferJobHandle));

                m_OverlayRenderSystem.AddBufferWriter(drawEdgesJobHandle);
                Dependency = drawEdgesJobHandle;
            }

            if (tool.RenderTempEdges) {
                var drawTempEdgesJob = new DrawTempEdgesJob {
                    m_Buffer                   = m_OverlayRenderSystem.GetBuffer(out var tempEdgeBufferJobHandle),
                    m_Colors                   = RenderColors.Default,
                    m_EdgeComponentTypeHandle  = SystemAPI.GetComponentTypeHandle<Edge>(),
                    m_CurveComponentTypeHandle = SystemAPI.GetComponentTypeHandle<Curve>(),
                    m_TempComponentTypeHandle  = SystemAPI.GetComponentTypeHandle<Temp>(),
                    m_TempLookup               = SystemAPI.GetComponentLookup<Temp>(true),
                };

                var drawTempEdgesJobHandle = drawTempEdgesJob.ScheduleByRef(m_TempEdgeQuery,
                    JobHandle.CombineDependencies(Dependency,
                        tempEdgeBufferJobHandle));

                m_OverlayRenderSystem.AddBufferWriter(drawTempEdgesJobHandle);
                Dependency = drawTempEdgesJobHandle;
            }

            if (tool.RenderTempNodes) {
                var drawTempNodesJob = new DrawTempNodesJob {
                    m_Buffer                  = m_OverlayRenderSystem.GetBuffer(out var tempNodeBufferJobHandle),
                    m_Colors                  = RenderColors.Default,
                    m_TempComponentTypeHandle = SystemAPI.GetComponentTypeHandle<Temp>(),
                    m_NodeComponentTypeHandle = SystemAPI.GetComponentTypeHandle<Node>()
                };

                var drawTempNodesJobHandle = drawTempNodesJob.ScheduleByRef(m_TempNodeQuery,
                    JobHandle.CombineDependencies(Dependency,
                        tempNodeBufferJobHandle));

                m_OverlayRenderSystem.AddBufferWriter(drawTempNodesJobHandle);
                Dependency = drawTempNodesJobHandle;
            }
        }
    }
}