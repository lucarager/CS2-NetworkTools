// <copyright file="IntersectionEdgeAdjustment.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools.RoadShape {
    #region Using Statements

    using Colossal.Mathematics;
    using Unity.Entities;

    #endregion

    /// <summary>
    /// Represents an adjustment to a non-path edge connected to an intersection node.
    /// Used by both preview and apply output modes.
    /// </summary>
    public struct IntersectionEdgeAdjustment {
        /// <summary>
        /// The edge entity being adjusted.
        /// </summary>
        public Entity EdgeEntity;

        /// <summary>
        /// The adjusted bezier curve for this edge.
        /// </summary>
        public Bezier4x3 Bezier;

        /// <summary>
        /// The original length of this edge (preserved during adjustment).
        /// </summary>
        public float Length;

        /// <summary>
        /// The node on the path (intersection node) - should NOT be referenced in preview.
        /// </summary>
        public Entity PathNode;

        /// <summary>
        /// The node NOT on the path (far node) - should be referenced in preview to fix position.
        /// </summary>
        public Entity FarNode;

        /// <summary>
        /// True if the path node is at the start of the edge (bezier.a), false if at the end (bezier.d).
        /// </summary>
        public bool PathNodeIsStart;

        /// <summary>
        /// Network composition of the edge (Ground, Elevated, Tunnel).
        /// </summary>
        public NetworkComposition NetworkComposition;
    }
}
