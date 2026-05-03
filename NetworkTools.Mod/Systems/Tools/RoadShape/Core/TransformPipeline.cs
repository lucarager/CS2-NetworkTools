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
            ref NativeArray<NodeState> nodes,
            in ShapeTransformContext ctx,
            in ShapeJobConfig config)
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

            // 4. Compute node positions from edge transformation deltas
            ComputeNodePositions(ref nodes, in edges);
        }

        /// <summary>
        /// Computes new node positions by applying the transformation delta from
        /// edge bezier endpoints to the original node positions.
        /// Nodes are path-ordered: node[i] connects to edge[i-1]'s path-end and edge[i]'s path-start.
        /// For interior nodes with two adjacent edges, the delta is averaged.
        /// </summary>
        private static void ComputeNodePositions(ref NativeArray<NodeState> nodes, in NativeArray<EdgeState> edges) {
            for (var i = 0; i < nodes.Length; i++) {
                // First and last nodes are the user's selected path endpoints — they stay fixed
                if (i == 0 || i == nodes.Length - 1) {
                    continue;
                }

                var node = nodes[i];
                var delta = float3.zero;
                var contributors = 0;

                // Check previous edge (this node is its path-end)
                if (i > 0) {
                    var prevEdge = edges[i - 1];
                    // Path-end of previous edge: forward = bezier.d, reversed = bezier.a
                    if (prevEdge.IsForward) {
                        delta += prevEdge.Bezier.d - prevEdge.OriginalBezierD;
                    } else {
                        delta += prevEdge.Bezier.a - prevEdge.OriginalBezierA;
                    }
                    contributors++;
                }

                // Check next edge (this node is its path-start)
                if (i < edges.Length) {
                    var nextEdge = edges[i];
                    // Path-start of next edge: forward = bezier.a, reversed = bezier.d
                    if (nextEdge.IsForward) {
                        delta += nextEdge.Bezier.a - nextEdge.OriginalBezierA;
                    } else {
                        delta += nextEdge.Bezier.d - nextEdge.OriginalBezierD;
                    }
                    contributors++;
                }

                if (contributors > 0) {
                    node.Position = node.OriginalPosition + delta / contributors;
                }

                nodes[i] = node;
            }
        }
    }
}
