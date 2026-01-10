// <copyright file="NT_RenderSystem.cs" company="Luca Rager">
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
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Tools;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Utils;
    using Color = UnityEngine.Color;

    #endregion

    /// <summary>
    /// Overlay Rendering System.
    /// </summary>
    public partial class NT_RenderSystem : GameSystemBase {
        private EntityQuery         m_NodeQuery;
        private EntityQuery         m_EdgeQuery;
        private OverlayRenderSystem m_OverlayRenderSystem;
        private PreCullingSystem    m_PreCullingSystem;
        private PrefixedLogger      m_Log;
        private ToolSystem          m_ToolSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            // Logger
            m_Log = new PrefixedLogger(nameof(NT_RenderSystem));
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

            // Systems & References
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            m_PreCullingSystem    = World.GetOrCreateSystemManaged<PreCullingSystem>();
            m_ToolSystem          = World.GetOrCreateSystemManaged<ToolSystem>();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            if (m_ToolSystem.activeTool is not NT_BaseToolSystem tool) {
                return;
            }

            if (tool.ShowNodes) {
                var nodeBufferJobHandle = default(JobHandle);
                var edgeBufferJobHandle = default(JobHandle);

                // Draw nodes
                var drawNodesJob = new DrawNodesJob {
                    m_Buffer                         = m_OverlayRenderSystem.GetBuffer(out nodeBufferJobHandle),
                    m_HighlightedComponentTypeHandle = SystemAPI.GetComponentTypeHandle<NT_Highlighted>(),
                    m_SelectedComponentTypeHandle    = SystemAPI.GetComponentTypeHandle<NT_Selected>(),
                    m_EligibleComponentTypeHandle    = SystemAPI.GetComponentTypeHandle<NT_Eligible>(),
                    m_SelectedFirstComponentTypeHandle = SystemAPI.GetComponentTypeHandle<NT_SelectedFirst>(),
                    m_SelectedLastComponentTypeHandle  = SystemAPI.GetComponentTypeHandle<NT_SelectedLast>(),
                    m_NodeComponentTypeHandle        = SystemAPI.GetComponentTypeHandle<Node>(),
                };

                var drawNodesJobHandle = drawNodesJob.ScheduleByRef(
                    m_NodeQuery,
                    JobHandle.CombineDependencies(
                        Dependency,
                        nodeBufferJobHandle
                    ));

                m_OverlayRenderSystem.AddBufferWriter(drawNodesJobHandle);

                // Draw edges
                var drawEdgesJob = new DrawEdgesJob {
                    m_Buffer                         = m_OverlayRenderSystem.GetBuffer(out edgeBufferJobHandle),
                    m_HighlightedComponentTypeHandle = SystemAPI.GetComponentTypeHandle<NT_Highlighted>(),
                    m_SelectedComponentTypeHandle    = SystemAPI.GetComponentTypeHandle<NT_Selected>(),
                    m_EdgeComponentTypeHandle = SystemAPI.GetComponentTypeHandle<Edge>(),
                    m_CurveComponentTypeHandle = SystemAPI.GetComponentTypeHandle<Curve>(),
                    m_NodeLookup = SystemAPI.GetComponentLookup<Node>(true),
                };

                var drawEdgesJobHandle = drawEdgesJob.ScheduleByRef(
                    m_EdgeQuery,
                    JobHandle.CombineDependencies(
                        drawNodesJobHandle,
                        edgeBufferJobHandle
                    ));

                m_OverlayRenderSystem.AddBufferWriter(drawEdgesJobHandle);
                
                Dependency = drawEdgesJobHandle;
            }
        }

        /// <summary>
        /// Job to draw node overlays.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
        protected struct DrawNodesJob : IJobChunk {
            [ReadOnly] public required OverlayRenderSystem.Buffer          m_Buffer;
            [ReadOnly] public required ComponentTypeHandle<NT_Highlighted> m_HighlightedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Selected>    m_SelectedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Eligible>    m_EligibleComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_SelectedFirst> m_SelectedFirstComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_SelectedLast>  m_SelectedLastComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Node> m_NodeComponentTypeHandle;

            /// <inheritdoc/>
            public void Execute(in ArchetypeChunk chunk,
                                int               unfilteredChunkIndex,
                                bool              useEnabledMask,
                                in v128           chunkEnabledMask) {
                var nodesArray = chunk.GetNativeArray(ref m_NodeComponentTypeHandle);

                for (var i = 0; i < nodesArray.Length; i++) {
                    var node = nodesArray[i];
                    
                    // Check component flags
                    var isHighlighted   = chunk.Has(ref m_HighlightedComponentTypeHandle);
                    var isSelected      = chunk.Has(ref m_SelectedComponentTypeHandle);
                    var isEligible      = chunk.Has(ref m_EligibleComponentTypeHandle);
                    var isSelectedFirst = chunk.Has(ref m_SelectedFirstComponentTypeHandle);
                    var isSelectedLast  = chunk.Has(ref m_SelectedLastComponentTypeHandle);

                    // Determine visual style based on node state
                    Color fillColor;
                    Color borderColor;
                    float radius;
                    float borderWidth;

                    if (isSelectedFirst) {
                        // First selected node - bright green
                        fillColor   = new Color(0.2f, 1f, 0.2f, 0.6f);
                        borderColor = new Color(0.2f, 1f, 0.2f, 1f);
                        radius      = 12f;
                        borderWidth = 0.5f;
                    } else if (isSelectedLast) {
                        // Last selected node - bright blue
                        fillColor   = new Color(0.2f, 0.5f, 1f, 0.6f);
                        borderColor = new Color(0.2f, 0.5f, 1f, 1f);
                        radius      = 12f;
                        borderWidth = 0.5f;
                    } else if (isSelected) {
                        // Intermediate path nodes - pink/rose
                        fillColor   = new Color(0.86f, 0.34f, 0.50f, 0.5f);
                        borderColor = new Color(0.86f, 0.34f, 0.50f, 0.9f);
                        radius      = 10f;
                        borderWidth = 0.4f;
                    } else if (isHighlighted) {
                        // Hovered eligible node or path nodes - yellow/gold
                        fillColor   = new Color(1f, 0.9f, 0.2f, 0.5f);
                        borderColor = new Color(1f, 0.9f, 0.2f, 0.95f);
                        radius      = 10f;
                        borderWidth = 0.4f;
                    } else if (isEligible) {
                        // Eligible but not hovered - white/subtle
                        fillColor   = new Color(1f, 1f, 1f, 0.2f);
                        borderColor = new Color(1f, 1f, 1f, 0.6f);
                        radius      = 8f;
                        borderWidth = 0.3f;
                    } else {
                        // Not eligible - don't render
                        continue;
                    }

                    m_Buffer.DrawCircle(
                        borderColor,
                        fillColor,
                        borderWidth,
                        OverlayRenderSystem.StyleFlags.Projected,
                        default,
                        node.m_Position,
                        radius
                    );
                }
            }
        }

        /// <summary>
        /// Job to draw edge overlays.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
        protected struct DrawEdgesJob : IJobChunk {
            [ReadOnly] public required OverlayRenderSystem.Buffer          m_Buffer;
            [ReadOnly] public required ComponentTypeHandle<NT_Highlighted> m_HighlightedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Selected>    m_SelectedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Edge> m_EdgeComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Curve> m_CurveComponentTypeHandle;
            [ReadOnly] public required ComponentLookup<Node>               m_NodeLookup;

            /// <inheritdoc/>
            public void Execute(in ArchetypeChunk chunk,
                                int               unfilteredChunkIndex,
                                bool              useEnabledMask,
                                in v128           chunkEnabledMask) {
                var edgesArray = chunk.GetNativeArray(ref m_EdgeComponentTypeHandle);
                var curvesArray = chunk.GetNativeArray(ref m_CurveComponentTypeHandle);

                for (var i = 0; i < edgesArray.Length; i++) {
                    var edge = edgesArray[i];
                    var curve = curvesArray[i];

                    // Check component flags
                    var isHighlighted = chunk.Has(ref m_HighlightedComponentTypeHandle);
                    var isSelected    = chunk.Has(ref m_SelectedComponentTypeHandle);

                    // Determine visual style based on edge state
                    Color color;
                    float width;

                    if (isSelected) {
                        // Selected edge - pink/rose
                        color = new Color(0.86f, 0.34f, 0.50f, 0.9f);
                        width = 4f;
                    } else if (isHighlighted) {
                        // Hovered/highlighted edge - yellow/gold
                        color = new Color(1f, 0.9f, 0.2f, 0.95f);
                        width = 4f;
                    } else {
                        // Not highlighted or selected - don't render
                        continue;
                    }

                    m_Buffer.DrawCurve(color, curve.m_Bezier, width);
                }
            }
        }
    }
}