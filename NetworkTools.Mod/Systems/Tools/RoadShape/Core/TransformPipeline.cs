namespace NetworkTools.Systems.Tools.RoadShape {
    using Unity.Collections;

    /// <summary>
    /// Executes a single transformation through all phases.
    /// </summary>
    public static class TransformPipeline {

        public static void Execute<T>(
            ref T transform,
            ref NativeArray<EdgeState> edges,
            in ShapeTransformContext ctx,
            in ShapeTransformConfig config)
            where T : struct, IPathTransformation {

            // 1. PreProcess (global calculations - may set instance fields like ReferenceBezier)
            transform.PreProcess(ref edges, in ctx, in config);

            // 2. Process each edge
            for (var i = 0; i < edges.Length; i++)
            {
                var edge = edges[i];
                transform.Process(ref edge, i, in ctx, in config);
                edges[i] = edge;
            }

            // 3. PostProcess (cleanup)
            transform.PostProcess(ref edges, in ctx, in config);
        }
    }
}
