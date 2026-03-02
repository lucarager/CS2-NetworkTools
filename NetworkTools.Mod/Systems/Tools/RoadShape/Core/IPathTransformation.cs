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
        /// <summary>
        /// Called once when the template is selected or path changes.
        /// Use to compute initial values that need to be stored in config
        /// for both handle creation and transform execution.
        /// </summary>
        /// <param name="ctx">The transform context (path geometry).</param>
        /// <param name="config">The transform configuration (can be modified to store computed values).</param>
        void InitializeConfig(in ShapeTransformContext ctx, ref ShapeTransformConfig config);

        /// <summary>
        /// Called before processing edges. Use for per-frame calculations
        /// that require access to all edges (e.g., calculating reference bezier from config values).
        /// </summary>
        /// <param name="edges">All edges in the path.</param>
        /// <param name="ctx">The transform context (path geometry).</param>
        /// <param name="config">The transform configuration (user settings).</param>
        void PreProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeTransformConfig config);

        /// <summary>
        /// Called for each edge in sequence. The main transformation logic.
        /// </summary>
        /// <param name="edge">The edge to transform (modified in place).</param>
        /// <param name="index">Index of this edge in the path.</param>
        /// <param name="ctx">The transform context (path geometry).</param>
        /// <param name="config">The transform configuration (user settings).</param>
        void Process(ref EdgeState edge, int index, in ShapeTransformContext ctx, in ShapeTransformConfig config);

        /// <summary>
        /// Called after all edges are processed. Use for cleanup or
        /// cross-edge adjustments.
        /// </summary>
        /// <param name="edges">All edges in the path.</param>
        /// <param name="ctx">The transform context (path geometry).</param>
        /// <param name="config">The transform configuration (user settings).</param>
        void PostProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeTransformConfig config);
    }

    /// <summary>
    /// Optional interface for transforms that support in-world handles.
    /// </summary>
    public interface IHandleableTransformation : IPathTransformation {
        /// <summary>
        /// Gets the handle definitions for this transform.
        /// Called by the tool system when creating handles.
        /// Values needed for handle positioning should already be in config (from InitializeConfig).
        /// </summary>
        /// <param name="ctx">The transform context (path geometry).</param>
        /// <param name="config">The transform configuration (contains computed values from InitializeConfig).</param>
        /// <param name="pathStartPos">World position of path start.</param>
        /// <param name="pathEndPos">World position of path end.</param>
        /// <param name="edgeStates">All edge states in the path (for direction calculations).</param>
        /// <returns>Array of handle definitions.</returns>
        TransformHandleDefinition[] GetHandleDefinitions(
            in ShapeTransformContext ctx,
            in ShapeTransformConfig config,
            float3 pathStartPos,
            float3 pathEndPos,
            in NativeArray<EdgeState> edgeStates);
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
