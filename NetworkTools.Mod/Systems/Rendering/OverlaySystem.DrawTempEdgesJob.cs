namespace NetworkTools.Systems {
    using LucaModsCommon.Rendering;
    using System.Diagnostics.CodeAnalysis;
    using Colossal.Mathematics;
    using Game.Net;
    using Game.Rendering;
    using Game.Tools;
    using NetworkTools.Components;
    using NetworkTools.Systems.Rendering;
    using Unity.Burst;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;

    public partial class NT_OverlaySystem {
        /// <summary>
        ///     Job to draw edge overlays.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if USE_BURST
        [BurstCompile]
#endif
        protected struct DrawTempEdgesJob : IJobChunk {
            [ReadOnly] public required CustomOverlayRenderSystem.Buffer m_Buffer;
            [ReadOnly] public required RenderColors m_Colors;
            [ReadOnly] public required RenderDimensions m_Dimensions;
            [ReadOnly] public required ComponentTypeHandle<Temp> m_TempComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Edge> m_EdgeComponentTypeHandle;
            [ReadOnly] public required ComponentTypeHandle<Curve> m_CurveComponentTypeHandle;
            [ReadOnly] public required ComponentLookup<Temp> m_TempLookup;
            [ReadOnly] public required ComponentLookup<Edge> m_EdgeLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge> m_ConnectedEdgeLookup;

            /// <inheritdoc />
            public void Execute(in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask) {
                var edgesArray = chunk.GetNativeArray(ref m_EdgeComponentTypeHandle);
                var curvesArray = chunk.GetNativeArray(ref m_CurveComponentTypeHandle);
                var tempArray = chunk.GetNativeArray(ref m_TempComponentTypeHandle);

                for (var i = 0; i < edgesArray.Length; i++) {
                    var edge = edgesArray[i];
                    var curve = curvesArray[i];
                    var temp = tempArray[i];

                    // Only draw the "new" edge (original is null) or if the edge is marked for replacement (flags has Replace).
                    if (temp.m_Original != Entity.Null || (temp.m_Flags & TempFlags.Replace) != 0) {
                        continue;
                    }

                    // Determine visual style
                    var color = (Color)(Vector4)m_Colors.TempEdge;
                    var width = 1f;
                    var perpLineHalfLength = 2f;

                    // Determine if start/end nodes are middle nodes 
                    var startNodeIsMiddle = m_TempLookup.TryGetComponent(edge.m_Start, out var startTemp) && m_EdgeLookup.HasComponent(startTemp.m_Original);
                    var endNodeIsMiddle = m_TempLookup.TryGetComponent(edge.m_End, out var endTemp) && m_EdgeLookup.HasComponent(endTemp.m_Original);

                    // Double the trim at intersections (non-deadends) to account for the cutoff
                    var absoluteTrim = 4f;
                    var relativeTrim = math.clamp(absoluteTrim / curve.m_Length, 0f, 0.49f);

                    // Convert to relative trim, clamped to 49% — only applied at middle nodes
                    var startTrim = startNodeIsMiddle ? relativeTrim : 0f;
                    var endTrim = endNodeIsMiddle ? relativeTrim : 0f;
                    var trimmedBezier = MathUtils.Cut(curve.m_Bezier, new Bounds1(startTrim, 1f - endTrim));

                    // Draw the shortened curve bezier
                    m_Buffer.DrawCurve(color, trimmedBezier, width, true);

                    // Draw perpendicular line at start 
                    var startPoint = trimmedBezier.a;
                    var startTangent = math.normalize(MathUtils.Tangent(trimmedBezier, 0f));
                    var startPerp = new float3(-startTangent.z, 0f, startTangent.x);
                    var startLine = new Line3.Segment(startPoint - startPerp * perpLineHalfLength,
                        startPoint + startPerp * perpLineHalfLength);
                    m_Buffer.DrawLine(color, startLine, width);

                    // Draw perpendicular line at end 
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