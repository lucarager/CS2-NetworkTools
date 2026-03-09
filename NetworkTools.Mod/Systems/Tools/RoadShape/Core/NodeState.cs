namespace NetworkTools.Systems.Tools.RoadShape {
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// Per-node state that flows through the transformation pipeline.
    /// Tracks node positions independently from edge bezier endpoints.
    /// A node's position represents the intersection center, which is
    /// independent from the bezier endpoints (where roads meet the intersection).
    /// </summary>
    public struct NodeState {
        // === Identity (immutable after creation) ===

        /// <summary>
        /// The node entity.
        /// </summary>
        public Entity Entity;

        /// <summary>
        /// Index of this node in the path (0 = first, N = last).
        /// For a path with N edges, there are N+1 nodes.
        /// </summary>
        public int PathIndex;

        // === Geometry (mutable - computed by pipeline after transforms) ===

        /// <summary>
        /// The computed node position after transformation.
        /// Derived by applying the bezier endpoint delta to the original node position.
        /// </summary>
        public float3 Position;

        // === Original values (immutable - for delta calculations) ===

        /// <summary>
        /// Original node position from the ECS Node component.
        /// Used as the base for applying transformation deltas.
        /// </summary>
        public float3 OriginalPosition;
    }
}
