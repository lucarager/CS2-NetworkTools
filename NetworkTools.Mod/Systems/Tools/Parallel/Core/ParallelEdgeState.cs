namespace NetworkTools.Systems.Tools.Parallel {
    using Colossal.Mathematics;
    using Unity.Entities;

    /// <summary>
    ///     Per-edge state collected during Phase 1 of the parallel offset pipeline
    ///     and consumed by subsequent phases. Mirrors the pattern used by
    ///     <see cref="RoadShape.EdgeState"/> in the RoadShape tool.
    /// </summary>
    internal struct ParallelEdgeState {
        // === Identity (immutable after creation) ===

        /// <summary>The edge entity being offset.</summary>
        public Entity EdgeEntity;

        /// <summary>Path-ordered start node entity.</summary>
        public Entity PathStartNode;

        /// <summary>Path-ordered end node entity.</summary>
        public Entity PathEndNode;

        /// <summary>True if the edge direction matches the path direction.</summary>
        public bool IsForward;

        /// <summary>True if the edge has valid curve data.</summary>
        public bool IsValid;

        // === Geometry (immutable after Phase 1) ===

        /// <summary>Path-ordered bezier curve of the original edge.</summary>
        public Bezier4x3 Bezier;

        /// <summary>Arc length of the original bezier.</summary>
        public float Length;
    }
}