namespace NetworkTools.Systems.Tools.RoadShape {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Colossal.Mathematics;
    using NetworkTools.Components;

    using Unity.Collections;
    using Unity.Mathematics;

    /// <summary>
    /// Interface for path transformation operations.
    /// Implemented by structs for Burst compatibility.
    /// </summary>
    public interface IPathTransformation {
        void Initialize(
            in ShapeTransformContext ctx,
            in Bezier4x3 firstEdgeBezier,
            bool firstEdgeIsForward,
            in Bezier4x3 lastEdgeBezier,
            bool lastEdgeIsForward);

        /// <summary>
        /// Called before processing edges. Use for global calculations
        /// that require access to all edges (e.g., calculating master bezier).
        /// </summary>
        /// <param name="edges">All edges in the path.</param>
        /// <param name="ctx">The transform context (may be modified).</param>
        void PreProcess(ref NativeArray<EdgeState> edges, ref ShapeTransformContext ctx);

        /// <summary>
        /// Called for each edge in sequence. The main transformation logic.
        /// </summary>
        /// <param name="edge">The edge to transform (modified in place).</param>
        /// <param name="index">Index of this edge in the path.</param>
        /// <param name="ctx">The transform context (read-only).</param>
        void Process(ref EdgeState edge, int index, in ShapeTransformContext ctx);

        /// <summary>
        /// Called after all edges are processed. Use for cleanup or
        /// cross-edge adjustments.
        /// </summary>
        /// <param name="edges">All edges in the path.</param>
        /// <param name="ctx">The transform context (read-only).</param>
        void PostProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx);
    }

    /// <summary>
    /// Optional interface for transforms that support in-world handles.
    /// </summary>
    public interface IHandleableTransformation : IPathTransformation {
        /// <summary>
        /// Gets the handle definitions for this transform.
        /// Called by the tool system when creating handles.
        /// </summary>
        /// <param name="ctx">The transform context.</param>
        /// <param name="pathStartPos">World position of path start.</param>
        /// <param name="pathEndPos">World position of path end.</param>
        /// <returns>Array of handle definitions.</returns>
        TransformHandleDefinition[] GetHandleDefinitions(
            in ShapeTransformContext ctx,
            float3 pathStartPos,
            float3 pathEndPos);
    }

    /// <summary>
    /// Definition for a transform handle.
    /// </summary>
    public struct TransformHandleDefinition {
        public int Key;
        public float3 Position;
        public HandleTypeFlags TypeFlags;
        public float Value;        // For parameter handles
        public float MinValue;
        public float MaxValue;
        public NT_HandleConstraints? Constraints;
    }
}
