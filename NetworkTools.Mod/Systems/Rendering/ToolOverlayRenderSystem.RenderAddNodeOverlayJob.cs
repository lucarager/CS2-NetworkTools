namespace NetworkTools.Systems {
    using System.Diagnostics.CodeAnalysis;
    using Colossal.Mathematics;
    using Game.Net;
    using Game.Tools;
    using NetworkTools.Components;
    using NetworkTools.Systems.Rendering;
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
#if BURST
        [BurstCompile]
#endif
        protected struct RenderAddNodeOverlayJob : IJobChunk {
            [ReadOnly] public required RenderColors                        m_Colors;
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
                var curvesArray = chunk.GetNativeArray(ref m_CurveComponentTypeHandle);

                for (var i = 0; i < entities.Length; i++) {
                    if (m_TempOriginals.Contains(entities[i])) {
                        continue;
                    }

                    if (!m_VisibleEntities.Contains(entities[i])) {
                        continue;
                    }

                    var color = (Color)(Vector4)m_Colors.AddNodeEdgeEligible;
                    DrawCurveWithEndCaps(color, curvesArray[i].m_Bezier);
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
                        DrawCurveWithEndCaps((Color)(Vector4)m_Colors.AddNodeEdgeEligible, curve.m_Bezier);
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

                    var color = (Color)(Vector4)m_Colors.AddNodeEdgeTemp;
                    DrawCurveWithEndCaps(color, trimmedBezier);

                    var nodeColor = (Color)(Vector4)m_Colors.AddNodeNode;

                    if (m_TempLookup.TryGetComponent(edge.m_Start, out var startNodeTemp)
                        && (startNodeTemp.m_Flags & TempFlags.Replace) != 0
                        && m_NodeLookup.TryGetComponent(edge.m_Start, out var startNode)) {
                        m_Buffer.DrawCircle(nodeColor, startNode.m_Position, 3f);
                    }

                    if (m_TempLookup.TryGetComponent(edge.m_End, out var endNodeTemp)
                        && (endNodeTemp.m_Flags & TempFlags.Replace) != 0
                        && m_NodeLookup.TryGetComponent(edge.m_End, out var endNode)) {
                        m_Buffer.DrawCircle(nodeColor, endNode.m_Position, 3f);
                    }
                }
            }

            private void DrawCurveWithEndCaps(Color color, Bezier4x3 bezier) {
                var width              = 1f;
                var perpLineHalfLength = 2f;

                m_Buffer.DrawCurve(color, bezier, width, true);

                var startTangent = math.normalize(MathUtils.Tangent(bezier, 0f));
                var startPerp    = new float3(-startTangent.z, 0f, startTangent.x);
                m_Buffer.DrawLine(color, new Line3.Segment(bezier.a - startPerp * perpLineHalfLength, bezier.a + startPerp * perpLineHalfLength), width);

                var endTangent = math.normalize(MathUtils.Tangent(bezier, 1f));
                var endPerp    = new float3(-endTangent.z, 0f, endTangent.x);
                m_Buffer.DrawLine(color, new Line3.Segment(bezier.d - endPerp * perpLineHalfLength, bezier.d + endPerp * perpLineHalfLength), width);
            }
        }
    }
}
