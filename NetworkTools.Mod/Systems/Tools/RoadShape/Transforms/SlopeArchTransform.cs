namespace NetworkTools.Systems.Tools.RoadShape {
    using Unity.Collections;
    using Unity.Mathematics;

    /// <summary>
    /// Applies an arch ("bridge span") slope: the path rises (or sags) to a target height at a
    /// chosen position, then returns to the end height. The crest height is measured relative to the
    /// mid-height between the start and end nodes, so 0 sits exactly between them.
    ///
    /// The curve is two parabolic halves that meet at a flat vertex placed at the arch position, so
    /// moving the position slides the crest along the path (a single parabola through the endpoints
    /// would keep its vertex centred and only change amplitude). Each edge's bezier height profile is
    /// fitted so the cubic passes through the curve at t = 0, 1/3, 2/3, 1 — precise even on a
    /// single-edge path.
    ///
    /// Smooth start/end optionally pin the first/last control point to the tangent of the single
    /// non-selected neighbor edge, matching the behaviour of the other slope modes.
    /// </summary>
    public struct SlopeArchTransform : IPathTransformation {
        private float m_StartHeight;
        private float m_PeakHeight;
        private float m_Position;
        private float m_LeftCurvature;   // A for r <= position: (startH - peakH) / position^2
        private float m_RightCurvature;  // A for r >  position: (endH   - peakH) / (1 - position)^2

        public void PreProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeJobConfig config) {
            m_StartHeight = ctx.StartHeight;
            var endHeight = ctx.EndPosition.y;

            // Crest height is relative to the mid-height between the endpoints.
            var midHeight = (m_StartHeight + endHeight) * 0.5f;
            m_PeakHeight  = midHeight + config.ArchHeight;
            m_Position    = math.clamp(config.ArchPosition, 0.001f, 0.999f);

            // Two parabolas sharing the vertex (position, peakHeight): the left one passes through
            // (0, startHeight), the right one through (1, endHeight). Both are flat at the vertex.
            m_LeftCurvature  = (m_StartHeight - m_PeakHeight) / (m_Position * m_Position);
            m_RightCurvature = (endHeight - m_PeakHeight) / ((1f - m_Position) * (1f - m_Position));
        }

        public void Process(ref EdgeState edge, int index, in ShapeTransformContext ctx, in ShapeJobConfig config) {
            var s0 = edge.StartPointAbsoluteRatio;
            var s1 = edge.EndPointAbsoluteRatio;

            // Sample the arch curve at the edge's endpoints and two interior thirds (path order).
            var startHeight = HeightAt(s0);
            var endHeight   = HeightAt(s1);
            var q1          = HeightAt(math.lerp(s0, s1, 1f / 3f));
            var q2          = HeightAt(math.lerp(s0, s1, 2f / 3f));

            // Solve the two control-point heights so the cubic passes through q1 at t=1/3 and q2 at
            // t=2/3 (Bernstein basis): B(1/3) = (8a+12b+6c+d)/27, B(2/3) = (a+6b+12c+8d)/27.
            var r1 = 27f * q1 - 8f * startHeight - endHeight;
            var r2 = 27f * q2 - startHeight - 8f * endHeight;
            var ctrlStartHeight = (2f * r1 - r2) / 18f;
            var ctrlEndHeight   = (2f * r2 - r1) / 18f;

            edge.Bezier = SlopeUtils.ApplyHeightsToBezier(
                edge.Bezier,
                startHeight,
                ctrlStartHeight,
                ctrlEndHeight,
                endHeight,
                edge.IsForward);
        }

        public void PostProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeJobConfig config) {
            // Smooth start/end: pin the first/last control point to the neighbor edge's tangent so
            // the arch blends into the connected road instead of chipping.
            if (edges.Length == 0) {
                return;
            }

            if (config.SmoothStart && ctx.StartSmoothEligible) {
                var first = edges[0];
                SlopeUtils.ApplySmoothStartControl(ref first, ctx.StartAnchorSlope);
                edges[0] = first;
            }

            if (config.SmoothEnd && ctx.EndSmoothEligible) {
                var lastIndex = edges.Length - 1;
                var last = edges[lastIndex];
                SlopeUtils.ApplySmoothEndControl(ref last, ctx.EndAnchorSlope);
                edges[lastIndex] = last;
            }
        }

        /// <summary>Height of the arch curve at a path ratio (0-1).</summary>
        private float HeightAt(float r) {
            var d = r - m_Position;
            var curvature = r <= m_Position ? m_LeftCurvature : m_RightCurvature;
            return m_PeakHeight + curvature * d * d;
        }
    }
}
