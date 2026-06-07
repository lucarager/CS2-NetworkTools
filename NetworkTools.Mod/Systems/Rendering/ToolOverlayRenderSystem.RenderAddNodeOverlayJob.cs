namespace NetworkTools.Systems {
    using LucaModsCommon.Rendering;
    using System.Diagnostics.CodeAnalysis;
    using Colossal.Mathematics;
    using Game.Net;
    using Game.Tools;
    using NetworkTools.Components;
    using NetworkTools.Systems.Rendering;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;

    public partial class NT_ToolOverlayRenderSystem {
        /// <summary>
        ///     Sequential job that iterates AddNode edge chunks and renders overlays
        ///     directly to the overlay buffer. Visibility is pre-determined by
        ///     <see cref="FrustumCullEntitiesJob"/>.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if USE_BURST
        [BurstCompile]
#endif
        protected struct RenderAddNodeOverlayJob : IJobChunk {
            [ReadOnly] public required EntityTypeHandle                    m_EntityTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Edge>           m_EdgeComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Curve>          m_CurveComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Eligible>    m_EligibleComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Highlighted> m_HighlightedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Selected>    m_SelectedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Temp>           m_TempComponentTypeHandle;
            [ReadOnly] public required NativeParallelHashSet<Entity>       m_TempOriginals;
            [ReadOnly] public required NativeParallelHashSet<Entity>       m_VisibleEntities;
            [ReadOnly] public required ComponentLookup<Node>               m_NodeLookup;
            [ReadOnly] public required ComponentLookup<Temp>               m_TempLookup;
            [ReadOnly] public required ComponentLookup<Edge>               m_EdgeLookup;
            [ReadOnly] public required ComponentLookup<NT_Eligible>        m_EligibleLookup;

            public required CustomOverlayRenderSystem.Buffer m_Buffer;

            /// <inheritdoc />
            public void Execute(in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask) {
                var isTemp = chunk.Has(ref m_TempComponentTypeHandle);

                if (isTemp) {
                    RenderTempEdges(in chunk);
                } else {
                    RenderEligibleEdges(in chunk);
                }
            }

            private void RenderEligibleEdges(in ArchetypeChunk chunk) {
                var entities    = chunk.GetNativeArray(m_EntityTypeHandle);
                var edgesArray  = chunk.GetNativeArray(ref m_EdgeComponentTypeHandle);
                var curvesArray = chunk.GetNativeArray(ref m_CurveComponentTypeHandle);

                for (var i = 0; i < entities.Length; i++) {
                    if (m_TempOriginals.Contains(entities[i])) {
                        continue;
                    }

                    if (!m_VisibleEntities.Contains(entities[i])) {
                        continue;
                    }

                    RenderVisibleEdge(edgesArray[i], curvesArray[i]);
                }
            }

            private void RenderVisibleEdge(in Edge edge, in Curve curve) {
                m_Buffer.DrawCurve(NT_Colors.EdgeVisible.Rest.Fill, curve.m_Bezier, 1f, true);

                if (m_NodeLookup.TryGetComponent(edge.m_Start, out var startNode)) {
                    m_Buffer.DrawCircle(NT_Colors.NodeVisible.Rest.Fill, startNode.m_Position, 3f);
                }

                if (m_NodeLookup.TryGetComponent(edge.m_End, out var endNode)) {
                    m_Buffer.DrawCircle(NT_Colors.NodeVisible.Rest.Fill, endNode.m_Position, 3f);
                }
            }

            private void RenderTempEdges(in ArchetypeChunk chunk) {
                var edgesArray  = chunk.GetNativeArray(ref m_EdgeComponentTypeHandle);
                var curvesArray = chunk.GetNativeArray(ref m_CurveComponentTypeHandle);
                var tempArray   = chunk.GetNativeArray(ref m_TempComponentTypeHandle);

                for (var i = 0; i < edgesArray.Length; i++) {
                    var edge  = edgesArray[i];
                    var curve = curvesArray[i];
                    var temp  = tempArray[i];

                    if (temp.m_Original != Entity.Null || (temp.m_Flags & TempFlags.Replace) != 0) {
                        if (m_EligibleLookup.HasComponent(temp.m_Original)) {
                            RenderVisibleEdge(edge, curve);
                        }
                        continue;
                    }

                    var startNodeIsMiddle = m_TempLookup.TryGetComponent(edge.m_Start, out var startTemp)
                                            && m_EdgeLookup.HasComponent(startTemp.m_Original);
                    var endNodeIsMiddle = m_TempLookup.TryGetComponent(edge.m_End, out var endTemp)
                                          && m_EdgeLookup.HasComponent(endTemp.m_Original);

                    var absoluteTrim  = 4f;
                    var relativeTrim  = math.clamp(absoluteTrim / curve.m_Length, 0f, 0.49f);
                    var startTrim     = startNodeIsMiddle ? relativeTrim : 0f;
                    var endTrim       = endNodeIsMiddle ? relativeTrim : 0f;
                    var trimmedBezier = MathUtils.Cut(curve.m_Bezier, new Bounds1(startTrim, 1f - endTrim));

                    m_Buffer.DrawCurve(NT_Colors.EdgeEligible.Active.Fill, trimmedBezier, 1f, true);

                    if (startNodeIsMiddle) {
                        var tangent = math.normalize(MathUtils.Tangent(trimmedBezier, 0f));
                        var perp    = new float3(-tangent.z, 0f, tangent.x);
                        m_Buffer.DrawLine(NT_Colors.EdgeEligible.Active.Fill, new Line3.Segment(trimmedBezier.a - perp * 2f, trimmedBezier.a + perp * 2f), 1f);
                    }

                    if (endNodeIsMiddle) {
                        var tangent = math.normalize(MathUtils.Tangent(trimmedBezier, 1f));
                        var perp    = new float3(-tangent.z, 0f, tangent.x);
                        m_Buffer.DrawLine(NT_Colors.EdgeEligible.Active.Fill, new Line3.Segment(trimmedBezier.d - perp * 2f, trimmedBezier.d + perp * 2f), 1f);
                    }


                    if (m_TempLookup.TryGetComponent(edge.m_Start, out var startNodeTemp)
                        && (startNodeTemp.m_Flags & TempFlags.Replace) != 0
                        && m_NodeLookup.TryGetComponent(edge.m_Start, out var startNode)) {
                        m_Buffer.DrawCircle(NT_Colors.NodeAdded.Rest.Fill, startNode.m_Position, 3f);
                    }

                    if (m_TempLookup.TryGetComponent(edge.m_End, out var endNodeTemp)
                        && (endNodeTemp.m_Flags & TempFlags.Replace) != 0
                        && m_NodeLookup.TryGetComponent(edge.m_End, out var endNode)) {
                        m_Buffer.DrawCircle(NT_Colors.NodeAdded.Rest.Fill, endNode.m_Position, 3f);
                    }
                }
            }

        }
    }
}
