namespace NetworkTools.Systems.Tools.RoadShape {
    using Colossal.Mathematics;

    using Unity.Collections;
    using Unity.Mathematics;

    /// <summary>
    /// Applies a linear slope - constant grade throughout the path.
    /// </summary>
    public struct SlopeLinearTransform : IPathTransformation {
        public Bezier4x3 ReferenceBezier;

        public void InitializeConfig(in ShapeTransformContext ctx, ref ShapeJobConfig config) {
            // No initialization needed - linear slope uses path endpoints directly
        }

        public void PreProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeJobConfig config) {
            // Construct a reference bezier that goes directly from path start to path end,
            // with control points at 1/3 and 2/3 of the way along the line. 
            var a = ctx.StartPosition;
            var d = ctx.EndPosition;
            var b = a + (d - a) / 3f;
            var c = a + 2f * (d - a) / 3f;
            ReferenceBezier = new Bezier4x3(a, b, c, d);
        }

        public void Process(ref EdgeState edge, int index, in ShapeTransformContext ctx, in ShapeJobConfig config) {
            edge.Bezier = SlopeUtils.ApplyHeightsToBezier(
                edge.Bezier,
                SlopeUtils.GetHeightAtCurvePosition(ReferenceBezier, edge.StartPointAbsoluteRatio),
                SlopeUtils.GetHeightAtCurvePosition(ReferenceBezier, edge.StartControlPointAbsoluteRatio),
                SlopeUtils.GetHeightAtCurvePosition(ReferenceBezier, edge.EndControlPointAbsoluteRatio),
                SlopeUtils.GetHeightAtCurvePosition(ReferenceBezier, edge.EndPointAbsoluteRatio),
                edge.IsForward);
        }

        public void PostProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeJobConfig config) {
            // no-op
        }
    }
}
