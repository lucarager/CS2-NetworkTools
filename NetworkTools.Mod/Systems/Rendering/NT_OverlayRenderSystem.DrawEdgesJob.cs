namespace NetworkTools.Systems {
    using System.Diagnostics.CodeAnalysis;
    using Game.Net;
    using Game.Rendering;
    using NetworkTools.Components;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using UnityEngine;

    public partial class NT_OverlayRenderSystem {
        /// <summary>
        ///     Job to draw edge overlays.
        /// </summary>
        [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
#if BURST
        [BurstCompile]
#endif
        protected struct DrawEdgesJob : IJobChunk {
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
                    var edge = edgesArray[i];
                    var curve = curvesArray[i];
                    var edgeGeometry = edgeGeometriesArray[i];
                    var startNodeGeometry = startNodeGeometriesArray[i];
                    var endNodeGeometry = endNodeGeometriesArray[i];

                    // Check component flags
                    var isHighlighted = chunk.Has(ref m_HighlightedComponentTypeHandle);
                    var isSelected = chunk.Has(ref m_SelectedComponentTypeHandle);

                    // Determine visual style based on edge state
                    Color color;
                    float width;

                    if (isSelected) {
                        // Selected edge - primary purple/bright
                        color = new Color(0.58f, 0.27f, 1f, 1f);
                        width = 2f;
                    }
                    else if (isHighlighted) {
                        // Hovered/highlighted edge - primary purple/subtle
                        color = new Color(0.58f, 0.27f, 1f, 1f);
                        width = 2f;
                    }
                    else {
                        // Not highlighted or selected - don't render
                        continue;
                    }

                    // Draw the curve bezier
                    m_Buffer.DrawCurve(color, curve.m_Bezier, width);

                    // Draw all curves in the EdgeGeometry
                    //m_Buffer.DrawCurve(color, edgeGeometry.m_Start.m_Left, width);
                    //m_Buffer.DrawCurve(color, edgeGeometry.m_Start.m_Right, width);
                    //m_Buffer.DrawCurve(color, edgeGeometry.m_End.m_Left, width);
                    //m_Buffer.DrawCurve(color, edgeGeometry.m_End.m_Right, width);

                    //m_Buffer.DrawCurve(Color.cyan, startNodeGeometry.m_Geometry.m_Middle, width);
                    //m_Buffer.DrawCurve(Color.red, startNodeGeometry.m_Geometry.m_Left.m_Left, width);
                    //m_Buffer.DrawCurve(Color.green, startNodeGeometry.m_Geometry.m_Left.m_Right, width);
                    //m_Buffer.DrawCurve(Color.white, startNodeGeometry.m_Geometry.m_Right.m_Left, width);
                    //m_Buffer.DrawCurve(Color.black, startNodeGeometry.m_Geometry.m_Right.m_Right, width);

                    //m_Buffer.DrawCurve(Color.gray, endNodeGeometry.m_Geometry.m_Middle, width);
                    //m_Buffer.DrawCurve(Color.blue, endNodeGeometry.m_Geometry.m_Left.m_Left, width);
                    //m_Buffer.DrawCurve(Color.yellow, endNodeGeometry.m_Geometry.m_Left.m_Right, width);
                    //m_Buffer.DrawCurve(Color.magenta, endNodeGeometry.m_Geometry.m_Right.m_Left, width);
                    //m_Buffer.DrawCurve(Color.gray, endNodeGeometry.m_Geometry.m_Right.m_Right, width);
                }
            }
        }
    }
}