namespace NetworkTools.Systems.Tools.RoadShape {
    using Unity.Collections;

    /// <summary>
    /// Executes a single transformation through all phases.
    /// </summary>
    public static class TransformPipeline {

        public static void Execute<T>(
            T transform,
            ref NativeArray<EdgeState> edges,
            ref ShapeTransformContext ctx)
            where T : struct, IPathTransformation {

            // 1. PreProcess (global calculations)
            transform.PreProcess(ref edges, ref ctx);

            // 2. Process each edge
            for (var i = 0; i < edges.Length; i++)
            {
                var edge = edges[i];
                transform.Process(ref edge, i, in ctx);
                edges[i] = edge;
            }

            // 3. PostProcess (cleanup)
            transform.PostProcess(ref edges, in ctx);
        }
    }
}
