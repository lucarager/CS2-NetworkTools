namespace NetworkTools.Systems.Tools.RoadShape {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Colossal.Mathematics;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// Per-edge state that flows through the transformation pipeline.
    /// Mutable - updated by each transform stage.
    /// </summary>
    public struct EdgeState {
        // === Identity (immutable after creation) ===

        /// <summary>
        /// The edge entity being transformed.
        /// </summary>
        public Entity EdgeEntity;

        /// <summary>
        /// The start node entity of the edge (in edge direction, not path direction).
        /// </summary>
        public Entity StartNode;

        /// <summary>
        /// The end node entity of the edge (in edge direction, not path direction).
        /// </summary>
        public Entity EndNode;

        /// <summary>
        /// Index of this edge in the path (0-based).
        /// </summary>
        public int PathIndex;

        /// <summary>
        /// True if the edge direction matches the path direction.
        /// </summary>
        public bool IsForward;

        /// <summary>
        /// Network composition of the edge.
        /// </summary>
        public NetworkComposition NetworkComposition;

        // === Geometry (mutable - updated by transforms) ===

        /// <summary>
        /// The current bezier curve. Updated by shape and slope transforms.
        /// </summary>
        public Bezier4x3 Bezier;

        /// <summary>
        /// Length of the edge.
        /// </summary>
        public float Length;

        /// <summary>
        /// Ratio (0-1) of the control point closer to path-start.
        /// Updated after shape transforms to reflect new positions.
        /// </summary>
        public float StartControlPointRatio;

        /// <summary>
        /// Ratio (0-1) of the control point closer to path-end.
        /// Updated after shape transforms to reflect new positions.
        /// </summary>
        public float EndControlPointRatio;

        /// <summary>
        /// Cumulative distance along the path at the start of this edge.
        /// </summary>
        public float CumulativeDistance;

        /// <summary>
        ///     Absolute ratio (0-1) of the edge's start point along the total path length.
        /// </summary>
        public float StartPointAbsoluteRatio;

        /// <summary>
        ///     Absolute ratio (0-1) of the edge's end point along the total path length.
        /// </summary>
        public float EndPointAbsoluteRatio;

        /// <summary>
        ///     Absolute ratio (0-1) of the edge's start control point (b) along the total path length.
        /// </summary>
        public float StartControlPointAbsoluteRatio;

        /// <summary>
        ///     Absolute ratio (0-1) of the edge's end control point (c) along the total path length.
        /// </summary>
        public float EndControlPointAbsoluteRatio;


        // === Original values (immutable - for node position delta calculations) ===

        /// <summary>
        /// Original bezier start point (a) before transformation.
        /// Used to compute the delta applied to node positions after transforms.
        /// </summary>
        public float3 OriginalBezierA;

        /// <summary>
        /// Original bezier end point (d) before transformation.
        /// Used to compute the delta applied to node positions after transforms.
        /// </summary>
        public float3 OriginalBezierD;

        /// <summary>
        /// Recalculates the control point ratios based on the current bezier geometry.
        /// Call this after modifying the bezier's XZ positions.
        /// </summary>
        public void CalculateControlPointRatios() {
            // Calculate bezier control point ratios based on horizontal distance from 'a'
            var horizontalA = new float2(Bezier.a.x, Bezier.a.z);
            var horizontalB = new float2(Bezier.b.x, Bezier.b.z);
            var horizontalC = new float2(Bezier.c.x, Bezier.c.z);

            float bRatio, cRatio;
            if (Length > 0.01f)
            {
                bRatio = math.clamp(math.distance(horizontalA, horizontalB) / Length, 0f, 1f);
                cRatio = math.clamp(math.distance(horizontalA, horizontalC) / Length, 0f, 1f);
            } else
            {
                bRatio = 1f / 3f;
                cRatio = 2f / 3f;
            }

            // Convert bezier ratios to path-ordered ratios
            // Forward: B is closer to path-start, C is closer to path-end
            // Reversed: C is closer to path-start, B is closer to path-end
            if (IsForward)
            {
                StartControlPointRatio = bRatio;
                EndControlPointRatio = cRatio;
            } else
            {
                StartControlPointRatio = 1f - cRatio;
                EndControlPointRatio = 1f - bRatio;
            }
        }
    }
}
