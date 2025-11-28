// <copyright file="NT_RenderSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using System.Diagnostics.CodeAnalysis;
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
                                   .WithNone<Deleted, Hidden>()
                                   .Build();

            // Systems & References
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            m_PreCullingSystem    = World.GetOrCreateSystemManaged<PreCullingSystem>();
            m_ToolSystem          = World.GetOrCreateSystemManaged<ToolSystem>();
        }

        /// <inheritdoc/>
        /// Should we ever want to do overlay rendering, check out GyzmoSystem as reference.
        protected override void OnUpdate() {
            if (m_ToolSystem.activeTool is not NT_BaseToolSystem tool) {
                return;
            }

            if (tool.ShowNodes) {
                var drawJob = new DrawNodesJob {
                    m_Buffer                         = m_OverlayRenderSystem.GetBuffer(out var bufferJobHandle),
                    m_HighlightedComponentTypeHandle = SystemAPI.GetComponentTypeHandle<NT_Highlighted>(),
                    m_SelectedComponentTypeHandle    = SystemAPI.GetComponentTypeHandle<NT_Selected>(),
                    m_NodeComponentTypeHandle        = SystemAPI.GetComponentTypeHandle<Node>(),
                };

                var drawJobHandle = drawJob.ScheduleByRef(
                    m_NodeQuery,
                    JobHandle.CombineDependencies(
                        Dependency,
                        bufferJobHandle
                    ));

                m_OverlayRenderSystem.AddBufferWriter(drawJobHandle);
                drawJobHandle.Complete();

                Dependency = drawJobHandle;
            }
        }

        /// <summary>
        /// Job to draw parcel overlays.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
        protected struct DrawNodesJob : IJobChunk {
            [ReadOnly] public required OverlayRenderSystem.Buffer          m_Buffer;
            [ReadOnly] public required ComponentTypeHandle<NT_Highlighted> m_HighlightedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Selected>    m_SelectedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Node>           m_NodeComponentTypeHandle;

            /// <inheritdoc/>
            public void Execute(in ArchetypeChunk chunk,
                                int               unfilteredChunkIndex,
                                bool              useEnabledMask,
                                in v128           chunkEnabledMask) {
                var nodesArray = chunk.GetNativeArray(ref m_NodeComponentTypeHandle);

                for (var i = 0; i < nodesArray.Length; i++) {
                    var node          = nodesArray[i];
                    var isHighlighted = chunk.Has(ref m_HighlightedComponentTypeHandle);
                    var isSelected    = chunk.Has(ref m_SelectedComponentTypeHandle);

                    var fillOpacity   = isHighlighted ? 0.5f : 0.2f;
                    var borderOpacity = isHighlighted ? 0.95f : 0.75f;
                    var fillColor     = isSelected ? new Color(0.86f, 0.34f, 0.50f, fillOpacity) : new Color(1f, 1f, 1f, fillOpacity);
                    var borderColor   = isSelected ? new Color(0.86f, 0.34f, 0.50f, borderOpacity) : new Color(1f, 1f, 1f, borderOpacity);

                    m_Buffer.DrawCircle(
                        borderColor,
                        fillColor,
                        0.4f,
                        OverlayRenderSystem.StyleFlags.Projected,
                        default,
                        node.m_Position,
                        10f
                    );
                }

                if (nodesArray.Length > 0) { }
            }
        }
    }
}