namespace NetworkTools.Systems.Tools.RoadShape {
    using Colossal.Mathematics;

    using Unity.Collections;
    using Unity.Mathematics;

    /// <summary>
    /// Straightens all edges to lie on a direct line from path start to path end.
    /// </summary>
    public struct SlopeLinearTransform : IPathTransformation {
        public ShapeTransformConfig Config;
        
        // "Reference" bezier
        public Bezier4x3 ReferenceBezier;

        public void Initialize(
            in ShapeTransformContext ctx,
            in Bezier4x3        firstEdgeBezier,
            bool                firstEdgeIsForward,
            in Bezier4x3        lastEdgeBezier,
            bool                lastEdgeIsForward) {

        }

        public void PreProcess(ref NativeArray<EdgeState> edges, ref ShapeTransformContext ctx) {
            // Construct a reference bezier that goes directly from path start to path end, with control points at 1/3 and 2/3 of the way along the line. 
            var a = ctx.StartPosition;
            var d = ctx.EndPosition;
            var b = a + (d - a) / 3f;
            var c = a + 2f * (d - a) / 3f;
            ReferenceBezier = new Bezier4x3(a, b, c, d);
            // no-op
        }

        public void Process(ref EdgeState edge, int index, in ShapeTransformContext ctx) {
            edge.Bezier.a.y = SlopeUtils.GetHeightAtCurvePosition(ReferenceBezier, edge.StartPointAbsoluteRatio);
            edge.Bezier.b.y = SlopeUtils.GetHeightAtCurvePosition(ReferenceBezier, edge.StartControlPointAbsoluteRatio);
            edge.Bezier.c.y = SlopeUtils.GetHeightAtCurvePosition(ReferenceBezier, edge.EndControlPointAbsoluteRatio);
            edge.Bezier.d.y = SlopeUtils.GetHeightAtCurvePosition(ReferenceBezier, edge.EndPointAbsoluteRatio);
        }

        public void PostProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx) {
            // no-op
        }
    }
}
