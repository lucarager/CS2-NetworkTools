namespace NetworkTools.Systems {
    using System.Diagnostics.CodeAnalysis;
    using Colossal.Mathematics;
    using Game.Net;
    using Game.Rendering;
    using NetworkTools.Components;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;

    public partial class NT_OverlayRenderSystem {
        /// <summary>
        ///     Job to draw edge overlays.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if BURST
        [BurstCompile]
#endif
        protected struct DrawTempEdgesJob : IJobChunk {
            [ReadOnly] public required OverlayRenderSystem.Buffer m_Buffer;
            [ReadOnly] public required ComponentTypeHandle<NT_Highlighted> m_HighlightedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<NT_Selected> m_SelectedComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Edge> m_EdgeComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Curve> m_CurveComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<EdgeGeometry> m_EdgeGeometryComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<StartNodeGeometry> m_StartNodeGeometryComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<EndNodeGeometry> m_EndNodeGeometryComponentTypeHandle;
            [ReadOnly] public required ComponentLookup<Node> m_NodeLookup;

            /// <inheritdoc />
            public void Execute(in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask) {
                var edgesArray = chunk.GetNativeArray(ref m_EdgeComponentTypeHandle);
                var curvesArray = chunk.GetNativeArray(ref m_CurveComponentTypeHandle);
                var edgeGeometriesArray = chunk.GetNativeArray(ref m_EdgeGeometryComponentTypeHandle);
                var startNodeGeometriesArray = chunk.GetNativeArray(ref m_StartNodeGeometryComponentTypeHandle);
                var endNodeGeometriesArray = chunk.GetNativeArray(ref m_EndNodeGeometryComponentTypeHandle);

                for (var i = 0; i < edgesArray.Length; i++) {
                    var curve = curvesArray[i];

                    // Determine visual style
                    var color = new Color(1f, 1f, 1f, 1f);
                    var width = 1f;
                    var perpLineHalfLength = 2f;

                    // Trim the bezier by a fixed absolute distance at each end
                    var absoluteTrimAmount = 4f;
                    var relativeTrim = math.clamp(absoluteTrimAmount / curve.m_Length, 0f, 0.49f);
                    var trimmedBezier = MathUtils.Cut(curve.m_Bezier, new Bounds1(relativeTrim, 1f - relativeTrim));

                    // Draw the shortened curve bezier
                    m_Buffer.DrawCurve(color, trimmedBezier, width);

                    // Calculate perpendicular lines at start
                    var startPoint = trimmedBezier.a;
                    var startTangent = math.normalize(MathUtils.Tangent(trimmedBezier, 0f));
                    var startPerp = new float3(-startTangent.z, 0f, startTangent.x);
                    var startLine = new Line3.Segment(startPoint - startPerp * perpLineHalfLength,
                        startPoint + startPerp * perpLineHalfLength);
                    m_Buffer.DrawLine(color, startLine, width);

                    // Calculate perpendicular lines at end
                    var endPoint = trimmedBezier.d;
                    var endTangent = math.normalize(MathUtils.Tangent(trimmedBezier, 1f));
                    var endPerp = new float3(-endTangent.z, 0f, endTangent.x);
                    var endLine = new Line3.Segment(endPoint - endPerp * perpLineHalfLength,
                        endPoint + endPerp * perpLineHalfLength);
                    m_Buffer.DrawLine(color, endLine, width);
                }
            }
        }
    }
}