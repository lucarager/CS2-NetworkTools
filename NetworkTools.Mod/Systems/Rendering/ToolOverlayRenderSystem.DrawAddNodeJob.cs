namespace NetworkTools.Systems {
    using System.Diagnostics.CodeAnalysis;
    using Colossal.Mathematics;
    using Game.Net;
    using Game.Rendering;
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
        ///     Combined overlay job for the AddNode tool.
        ///     Iterates edges matched by <c>WithAny&lt;NT_Eligible, NT_Highlighted, NT_Selected, Temp&gt;</c>
        ///     and branches to render either eligible-edge node indicators or temp-edge preview lines.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if BURST
        [BurstCompile]
#endif
        protected struct DrawAddNodeJob : IJobChunk {
            [ReadOnly] public required CustomOverlayRenderSystem.Buffer    m_Buffer;
            [ReadOnly] public required RenderColors                        m_Colors;
            [ReadOnly] public required RenderDimensions                    m_Dimensions;
            [ReadOnly] public required EntityTypeHandle                    m_EntityTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Edge>           m_EdgeComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Curve>          m_CurveComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<EdgeGeometry>   m_EdgeGeometryComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Eligible>    m_EligibleComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Highlighted> m_HighlightedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Selected>    m_SelectedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Temp>           m_TempComponentTypeHandle;
            [ReadOnly] public required NativeParallelHashSet<Entity>       m_TempOriginals;
            [ReadOnly] public required ComponentLookup<Node>               m_NodeLookup;
            [ReadOnly] public required ComponentLookup<EdgeGeometry>       m_EdgeGeometryLookup;
            [ReadOnly] public required ComponentLookup<Temp>               m_TempLookup;
            [ReadOnly] public required ComponentLookup<Edge>               m_EdgeLookup;

            /// <inheritdoc />
            public void Execute(in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask) {
                var isTemp = chunk.Has(ref m_TempComponentTypeHandle);

                if (isTemp) {
                    DrawTempEdges(in chunk);
                } else {
                    DrawEligibleEdges(in chunk);
                }
            }

            /// <summary>
            ///     Draws curve lines with perpendicular end-caps for eligible/highlighted/selected edges.
            ///     Edges whose entity is referenced as a temp original are skipped.
            /// </summary>
            private void DrawEligibleEdges(in ArchetypeChunk chunk) {
                var entities    = chunk.GetNativeArray(m_EntityTypeHandle);
                var curvesArray = chunk.GetNativeArray(ref m_CurveComponentTypeHandle);

                for (var i = 0; i < entities.Length; i++) {
                    // Skip edges that are currently represented by temp edges
                    if (m_TempOriginals.Contains(entities[i])) {
                        continue;
                    }

                    var color = (Color)(Vector4)m_Colors.AddNodeEdgeEligible;
                    DrawEdgeCurveWithEndCaps(color, curvesArray[i].m_Bezier);
                }
            }

            /// <summary>
            ///     Draws temp edge preview lines and temp node dots for the hovered edge.
            /// </summary>
            private void DrawTempEdges(in ArchetypeChunk chunk) {
                var edgesArray  = chunk.GetNativeArray(ref m_EdgeComponentTypeHandle);
                var curvesArray = chunk.GetNativeArray(ref m_CurveComponentTypeHandle);
                var tempArray   = chunk.GetNativeArray(ref m_TempComponentTypeHandle);

                for (var i = 0; i < edgesArray.Length; i++) {
                    var edge  = edgesArray[i];
                    var curve = curvesArray[i];
                    var temp  = tempArray[i];

                    // Only draw new edges (original is null) with custom logic that aren't replacements
                    if (temp.m_Original != Entity.Null || (temp.m_Flags & TempFlags.Replace) != 0) {
                        DrawEdgeCurveWithEndCaps((Vector4)m_Colors.AddNodeEdgeEligible, curvesArray[i].m_Bezier);

                        continue;
                    }

                    // Determine if start/end nodes are middle nodes
                    var startNodeIsMiddle = m_TempLookup.TryGetComponent(edge.m_Start, out var startTemp) && m_EdgeLookup.HasComponent(startTemp.m_Original);
                    var endNodeIsMiddle   = m_TempLookup.TryGetComponent(edge.m_End, out var endTemp) && m_EdgeLookup.HasComponent(endTemp.m_Original);

                    // Trim at middle nodes to account for the cutoff
                    var absoluteTrim  = 4f;
                    var relativeTrim  = math.clamp(absoluteTrim / curve.m_Length, 0f, 0.49f);
                    var startTrim     = startNodeIsMiddle ? relativeTrim : 0f;
                    var endTrim       = endNodeIsMiddle ? relativeTrim : 0f;
                    var trimmedBezier = MathUtils.Cut(curve.m_Bezier, new Bounds1(startTrim, 1f - endTrim));

                    var color = (Color)(Vector4)m_Colors.AddNodeEdgeTemp;
                    DrawEdgeCurveWithEndCaps(color, trimmedBezier);

                    // Draw temp node dot at the new middle node (Replace-flagged temp node)
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

            /// <summary>
            ///     Draws a curve along a bezier with perpendicular end-cap lines at both endpoints.
            /// </summary>
            private void DrawEdgeCurveWithEndCaps(Color color, Bezier4x3 bezier) {
                var width              = 1f;
                var perpLineHalfLength = 2f;

                // Draw the curve
                m_Buffer.DrawCurve(color, bezier, width, true);

                // Draw perpendicular line at start
                var startTangent = math.normalize(MathUtils.Tangent(bezier, 0f));
                var startPerp    = new float3(-startTangent.z, 0f, startTangent.x);
                var startLine    = new Line3.Segment(bezier.a - startPerp * perpLineHalfLength,
                                                     bezier.a + startPerp * perpLineHalfLength);
                m_Buffer.DrawLine(color, startLine, width);

                // Draw perpendicular line at end
                var endTangent = math.normalize(MathUtils.Tangent(bezier, 1f));
                var endPerp    = new float3(-endTangent.z, 0f, endTangent.x);
                var endLine    = new Line3.Segment(bezier.d - endPerp * perpLineHalfLength,
                                                   bezier.d + endPerp * perpLineHalfLength);
                m_Buffer.DrawLine(color, endLine, width);
            }
        }
    }
}
