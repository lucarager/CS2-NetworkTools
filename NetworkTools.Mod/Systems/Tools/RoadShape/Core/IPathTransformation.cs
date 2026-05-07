namespace NetworkTools.Systems.Tools.RoadShape {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Colossal.Mathematics;
    using NetworkTools.Components;
    using NetworkTools.Components.Handles;

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
        void InitializeConfig(in ShapeTransformContext ctx, ref ShapeJobConfig config);

        /// <summary>
        /// Called before processing edges. Use for per-frame calculations
        /// that require access to all edges (e.g., calculating reference bezier from config values).
        /// </summary>
        /// <param name="edges">All edges in the path.</param>
        /// <param name="ctx">The transform context (path geometry).</param>
        /// <param name="config">The transform configuration (user settings).</param>
        void PreProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeJobConfig config);

        /// <summary>
        /// Called for each edge in sequence. The main transformation logic.
        /// </summary>
        /// <param name="edge">The edge to transform (modified in place).</param>
        /// <param name="index">Index of this edge in the path.</param>
        /// <param name="ctx">The transform context (path geometry).</param>
        /// <param name="config">The transform configuration (user settings).</param>
        void Process(ref EdgeState edge, int index, in ShapeTransformContext ctx, in ShapeJobConfig config);

        /// <summary>
        /// Called after all edges are processed. Use for cleanup or
        /// cross-edge adjustments.
        /// </summary>
        /// <param name="edges">All edges in the path.</param>
        /// <param name="ctx">The transform context (path geometry).</param>
        /// <param name="config">The transform configuration (user settings).</param>
        void PostProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeJobConfig config);
    }

}
