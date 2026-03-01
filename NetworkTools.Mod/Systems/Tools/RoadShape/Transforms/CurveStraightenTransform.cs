namespace NetworkTools.Systems.Tools.RoadShape {
    using Colossal.Mathematics;

    using Unity.Collections;

    /// <summary>
    /// Straightens all edges to lie on a direct line from path start to path end.
    /// </summary>
    public struct CurveStraightenTransform : IPathTransformation {
        public ShapeTransformConfig Config;

        public void Initialize(
            in ShapeTransformContext ctx,
            in Bezier4x3        firstEdgeBezier,
            bool                firstEdgeIsForward,
            in Bezier4x3        lastEdgeBezier,
            bool                lastEdgeIsForward) {

        }

        public void PreProcess(ref NativeArray<EdgeState> edges, ref ShapeTransformContext ctx) {
        }

        public void Process(ref EdgeState edge, int index, in ShapeTransformContext ctx) {
        }

        public void PostProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx) {
        }
    }
}
