namespace NetworkTools.Systems.Tools.RoadShape {
    using Colossal.Mathematics;

    using Unity.Collections;

    /// <summary>
    /// Smooths curves by fitting edges to a smooth Bezier curve.
    /// </summary>
    public struct CurveSmoothTransform : IPathTransformation {
        public void InitializeConfig(in ShapeTransformContext ctx, ref ShapeTransformConfig config) {
            // TODO: Compute ideal bezier and store control points in config
            // This allows GetHandleDefinitions to position handles correctly
        }

        public void PreProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeTransformConfig config) {
            // TODO: Implement curve smoothing using config.ControlPointB/C
        }

        public void Process(ref EdgeState edge, int index, in ShapeTransformContext ctx, in ShapeTransformConfig config) {
            // TODO: Implement curve smoothing
        }

        public void PostProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeTransformConfig config) {
        }
    }
}
