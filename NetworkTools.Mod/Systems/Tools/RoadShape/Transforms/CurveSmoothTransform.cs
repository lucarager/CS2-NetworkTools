namespace NetworkTools.Systems.Tools.RoadShape {
    using Colossal.Mathematics;

    using Unity.Collections;

    /// <summary>
    /// Smooths curves by fitting edges to a smooth Bezier curve.
    /// </summary>
    public struct CurveSmoothTransform : IPathTransformation {
        public void PreProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeJobConfig config) {
            // TODO: Implement curve smoothing using config.ControlPointB/C
        }

        public void Process(ref EdgeState edge, int index, in ShapeTransformContext ctx, in ShapeJobConfig config) {
            // TODO: Implement curve smoothing
        }

        public void PostProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeJobConfig config) {
        }
    }
}
