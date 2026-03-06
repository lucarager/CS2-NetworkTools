namespace NetworkTools.Systems.Tools.RoadShape {
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

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

            // 4. Normalize shared node positions
            NormalizeNodePositions(ref edges);
        }

        /// <summary>
        /// Ensures that adjacent edges sharing a node use bit-identical endpoint positions.
        /// Transforms process edges independently, so floating-point differences can arise
        /// at shared nodes. This pass picks one position per node (first writer wins)
        /// and snaps all edge endpoints (a/d) to match.
        /// </summary>
        private static void NormalizeNodePositions(ref NativeArray<EdgeState> edges) {
            var nodePositions = new NativeHashMap<Entity, float3>(edges.Length * 2, Allocator.Temp);

            for (var i = 0; i < edges.Length; i++) {
                var state = edges[i];
                nodePositions.TryAdd(state.StartNode, state.Bezier.a);
                nodePositions.TryAdd(state.EndNode, state.Bezier.d);
            }

            for (var i = 0; i < edges.Length; i++) {
                var state    = edges[i];
                state.Bezier.a = nodePositions[state.StartNode];
                state.Bezier.d = nodePositions[state.EndNode];
                edges[i] = state;
            }

            nodePositions.Dispose();
        }
    }
}
