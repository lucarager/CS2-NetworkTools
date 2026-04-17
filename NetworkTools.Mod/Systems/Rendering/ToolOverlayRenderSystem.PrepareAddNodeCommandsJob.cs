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
        ///     Parallel job that iterates AddNode edge chunks, computes geometry,
        ///     and emits <see cref="OverlayDrawCommand"/>s into a <see cref="NativeStream"/>.
        ///     Visibility is pre-determined by <see cref="FrustumCullEntitiesJob"/>.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if BURST
        [BurstCompile]
#endif
        protected struct PrepareAddNodeCommandsJob : IJobChunk {
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

            public NativeStream.Writer m_CommandWriter;

            /// <inheritdoc />
            public void Execute(in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask) {
                m_CommandWriter.BeginForEachIndex(unfilteredChunkIndex);

                var isTemp = chunk.Has(ref m_TempComponentTypeHandle);

                if (isTemp) {
                    PrepareTempEdges(in chunk);
                } else {
                    PrepareEligibleEdges(in chunk);
                }

                m_CommandWriter.EndForEachIndex();
            }

            /// <summary>
            ///     Emits curve + end-cap commands for eligible edges, skipping temp originals and invisible entities.
            /// </summary>
            private void PrepareEligibleEdges(in ArchetypeChunk chunk) {
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
                    EmitCurveWithEndCaps(color, curvesArray[i].m_Bezier);
                }
            }

            /// <summary>
            ///     Emits commands for temp edge previews and temp node dots.
            /// </summary>
            private void PrepareTempEdges(in ArchetypeChunk chunk) {
                var edgesArray  = chunk.GetNativeArray(ref m_EdgeComponentTypeHandle);
                var curvesArray = chunk.GetNativeArray(ref m_CurveComponentTypeHandle);
                var tempArray   = chunk.GetNativeArray(ref m_TempComponentTypeHandle);

                for (var i = 0; i < edgesArray.Length; i++) {
                    var edge  = edgesArray[i];
                    var curve = curvesArray[i];
                    var temp  = tempArray[i];

                    // Replacement / existing-original edges: draw as eligible
                    if (temp.m_Original != Entity.Null || (temp.m_Flags & TempFlags.Replace) != 0) {
                        EmitCurveWithEndCaps((Color)(Vector4)m_Colors.AddNodeEdgeEligible, curve.m_Bezier);
                        continue;
                    }

                    // New temp edges: trim at middle nodes
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
                    EmitCurveWithEndCaps(color, trimmedBezier);

                    // Temp node dots at Replace-flagged nodes
                    var nodeColor = (Color)(Vector4)m_Colors.AddNodeNode;

                    if (m_TempLookup.TryGetComponent(edge.m_Start, out var startNodeTemp)
                        && (startNodeTemp.m_Flags & TempFlags.Replace) != 0
                        && m_NodeLookup.TryGetComponent(edge.m_Start, out var startNode)) {
                        m_CommandWriter.Write(new OverlayDrawCommand {
                            m_Type   = OverlayCommandType.Circle,
                            m_Color  = nodeColor,
                            m_PointA = startNode.m_Position,
                            m_Width  = 3f,
                        });
                    }

                    if (m_TempLookup.TryGetComponent(edge.m_End, out var endNodeTemp)
                        && (endNodeTemp.m_Flags & TempFlags.Replace) != 0
                        && m_NodeLookup.TryGetComponent(edge.m_End, out var endNode)) {
                        m_CommandWriter.Write(new OverlayDrawCommand {
                            m_Type   = OverlayCommandType.Circle,
                            m_Color  = nodeColor,
                            m_PointA = endNode.m_Position,
                            m_Width  = 3f,
                        });
                    }
                }
            }

            /// <summary>
            ///     Emits a curve command plus two perpendicular end-cap line commands.
            /// </summary>
            private void EmitCurveWithEndCaps(Color color, Bezier4x3 bezier) {
                var width              = 1f;
                var perpLineHalfLength = 2f;

                // Curve
                m_CommandWriter.Write(new OverlayDrawCommand {
                    m_Type    = OverlayCommandType.Curve,
                    m_Color   = color,
                    m_Bezier  = bezier,
                    m_Width   = width,
                    m_ForceUp = true,
                });

                // Start end-cap
                var startTangent = math.normalize(MathUtils.Tangent(bezier, 0f));
                var startPerp    = new float3(-startTangent.z, 0f, startTangent.x);

                m_CommandWriter.Write(new OverlayDrawCommand {
                    m_Type   = OverlayCommandType.Line,
                    m_Color  = color,
                    m_PointA = bezier.a - startPerp * perpLineHalfLength,
                    m_PointB = bezier.a + startPerp * perpLineHalfLength,
                    m_Width  = width,
                });

                // End end-cap
                var endTangent = math.normalize(MathUtils.Tangent(bezier, 1f));
                var endPerp    = new float3(-endTangent.z, 0f, endTangent.x);

                m_CommandWriter.Write(new OverlayDrawCommand {
                    m_Type   = OverlayCommandType.Line,
                    m_Color  = color,
                    m_PointA = bezier.d - endPerp * perpLineHalfLength,
                    m_PointB = bezier.d + endPerp * perpLineHalfLength,
                    m_Width  = width,
                });
            }
        }
    }
}
