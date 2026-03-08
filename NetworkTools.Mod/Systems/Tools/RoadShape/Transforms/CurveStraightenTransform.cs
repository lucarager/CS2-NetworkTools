namespace NetworkTools.Systems.Tools.RoadShape {
    using Colossal.Mathematics;

    using Unity.Collections;
    using Unity.Mathematics;

    /// <summary>
    /// Straightens all edges to lie on a direct line from path start to path end.
    /// Each edge becomes a straight bezier segment positioned along the line
    /// at its corresponding path ratio.
    /// </summary>
    public struct CurveStraightenTransform : IPathTransformation {
        public void InitializeConfig(in ShapeTransformContext ctx, ref ShapeTransformConfig config) {
            // No initialization needed - straighten uses path endpoints directly
        }

        public void PreProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeTransformConfig config) {
            // No global pre-processing needed - each edge is independently placed on the line
        }

        public void Process(ref EdgeState edge, int index, in ShapeTransformContext ctx, in ShapeTransformConfig config) {
            // Calculate new 3D positions on the straight line at path ratios
            var pathStartSidePos = math.lerp(ctx.StartPosition, ctx.EndPosition, edge.StartPointAbsoluteRatio);
            var pathEndSidePos   = math.lerp(ctx.StartPosition, ctx.EndPosition, edge.EndPointAbsoluteRatio);

            // Map path-ordered positions to bezier endpoints based on edge direction
            // Forward: bezier.a = path-start side, bezier.d = path-end side
            // Reversed: bezier.a = path-end side, bezier.d = path-start side
            float3 a, d;
            if (edge.IsForward) {
                a = pathStartSidePos;
                d = pathEndSidePos;
            } else {
                a = pathEndSidePos;
                d = pathStartSidePos;
            }

            // Place control points at 1/3 and 2/3 for a straight bezier segment
            var b = a + (d - a) / 3f;
            var c = a + 2f * (d - a) / 3f;

            edge.Bezier = new Bezier4x3(a, b, c, d);
        }

        public void PostProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeTransformConfig config) {
            // No post-processing needed
        }
    }
}
