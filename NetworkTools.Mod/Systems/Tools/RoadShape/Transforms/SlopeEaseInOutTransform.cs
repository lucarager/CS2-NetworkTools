namespace NetworkTools.Systems.Tools.RoadShape {
    using Colossal.Mathematics;

    using Unity.Collections;
    using Unity.Mathematics;

    /// <summary>
    /// Applies an ease-in-out slope - smooth transitions at start and end.
    /// Uses a bezier curve for height interpolation where the parameter t = path ratio.
    /// Control points determine the transition zones:
    /// - EaseInLength: how far along the path the slope starts to increase
    /// - EaseOutLength: how far from the end the slope starts to level off
    /// </summary>
    public struct SlopeEaseInOutTransform : IPathTransformation {
        /// <summary>
        /// Reference bezier curve used to sample heights at path ratios.
        /// Built in PreProcess with control points positioned to create the ease-in-out shape.
        /// </summary>
        public Bezier4x3 ReferenceBezier;

        public void InitializeConfig(in ShapeTransformContext ctx, ref ShapeJobConfig config) {
            // EaseInOut uses simple normalized parameters (0-0.5) stored in config.
            // No additional computed state needed - handles read directly from config.EaseInLength/EaseOutLength
        }

        public void PreProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeJobConfig config) {
            // Build a 2D height curve where x = path ratio (0-1) and y = height.
            // We solve for t where x(t) = pathRatio to get the correct height.
            //
            // Control point placement creates the S-curve:
            // - a: x=0, y=startHeight
            // - b: x=easeInLength, y=startHeight (flat tangent at start)
            // - c: x=(1-easeOutLength), y=endHeight (flat tangent at end)
            // - d: x=1, y=endHeight
            var a = new float3(0f, ctx.StartHeight, 0f);
            var b = new float3(config.EaseInLength, ctx.StartHeight, 0f);
            var c = new float3(1f - config.EaseOutLength, ctx.EndPosition.y, 0f);
            var d = new float3(1f, ctx.EndPosition.y, 0f);

            ReferenceBezier = new Bezier4x3(a, b, c, d);
        }

        public void Process(ref EdgeState edge, int index, in ShapeTransformContext ctx, in ShapeJobConfig config) {
            // Get heights at the actual node positions (start and end of edge in path order)
            var startHeight = SlopeUtils.GetHeightAtPathRatio(ReferenceBezier, edge.StartPointAbsoluteRatio);
            var endHeight = SlopeUtils.GetHeightAtPathRatio(ReferenceBezier, edge.EndPointAbsoluteRatio);

            // Get slopes at node positions - this ensures tangent continuity at shared nodes
            // Slope is in units of "height per path ratio" (not per world distance)
            var startSlope = SlopeUtils.GetSlopeAtPathRatio(ReferenceBezier, edge.StartPointAbsoluteRatio);
            var endSlope = SlopeUtils.GetSlopeAtPathRatio(ReferenceBezier, edge.EndPointAbsoluteRatio);

            // Calculate control point heights using path ratio differences (NOT world XZ distance)
            // The slope is dHeight/dPathRatio, so we multiply by the path ratio difference
            var ctrlStartHeight = startHeight + startSlope * (edge.StartControlPointAbsoluteRatio - edge.StartPointAbsoluteRatio);
            var ctrlEndHeight = endHeight + endSlope * (edge.EndControlPointAbsoluteRatio - edge.EndPointAbsoluteRatio);

            edge.Bezier = SlopeUtils.ApplyHeightsToBezier(
                edge.Bezier,
                startHeight,
                ctrlStartHeight,
                ctrlEndHeight,
                endHeight,
                edge.IsForward);
        }

        public void PostProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeJobConfig config) {
            // No post-processing needed
        }
    }
}
