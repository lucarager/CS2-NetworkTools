namespace NetworkTools.Systems.Tools.Connect {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Colossal.Mathematics;
    using Unity.Entities;
    using Unity.Mathematics;

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
