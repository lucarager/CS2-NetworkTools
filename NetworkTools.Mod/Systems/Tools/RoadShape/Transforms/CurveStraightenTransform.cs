namespace NetworkTools.Systems.Tools.RoadShape {
    using Colossal.Mathematics;

    using Unity.Collections;

    /// <summary>
    /// Straightens all edges to lie on a direct line from path start to path end.
    /// </summary>
    public struct CurveStraightenTransform : IPathTransformation {
        public void InitializeConfig(in ShapeTransformContext ctx, ref ShapeTransformConfig config) {
            // No initialization needed
        }

        public void PreProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeTransformConfig config) {
            // TODO: Implement curve straightening
        }

        public void Process(ref EdgeState edge, int index, in ShapeTransformContext ctx, in ShapeTransformConfig config) {
            // TODO: Implement curve straightening
        }

        public void PostProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeTransformConfig config) {
        }
    }
}
