namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Mathematics;
    using Unity.Entities;

    public struct CurveDef {
        /// <summary>
        /// The current bezier curve. Updated by shape and slope transforms.
        /// </summary>
        public Bezier4x3 Bezier;

        /// <summary>
        /// Length of the edge.
        /// </summary>
        public float Length;

        /// <summary>
        /// The start node entity of the edge. Will only be set for the first edge in a path.
        /// </summary>
        public Entity StartNodeEntity;

        /// <summary>
        /// The end node entity of the edge. Will only be set for the last edge in a path.
        /// </summary>
        public Entity EndNodeEntity;
    }
}
