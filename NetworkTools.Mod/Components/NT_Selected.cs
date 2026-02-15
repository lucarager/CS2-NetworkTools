// <copyright file="NT_Selected.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Components {
    #region Using Statements

    using Unity.Entities;

    #endregion

    /// <summary>
    /// Marks an entity as selected with its position in the selection path and render mode configuration.
    /// PathIndex indicates the order of traversal (0 = start, increasing towards end).
    /// </summary>
    public struct NT_Selected : IComponentData {
        public int            PathIndex;
        public NodeRenderMode NodeMode;
        public EdgeRenderMode EdgeMode;

        /// <summary>
        /// Creates a selection for a node with the specified render mode and path index.
        /// </summary>
        public static NT_Selected ForNode(NodeRenderMode mode, int pathIndex = 0) =>
            new() { PathIndex = pathIndex, NodeMode = mode, EdgeMode = EdgeRenderMode.None };

        /// <summary>
        /// Creates a selection for an edge with the specified render mode and path index.
        /// </summary>
        public static NT_Selected ForEdge(EdgeRenderMode mode, int pathIndex = 0) =>
            new() { PathIndex = pathIndex, NodeMode = NodeRenderMode.None, EdgeMode = mode };

        /// <summary>
        /// Default node selection (circle).
        /// </summary>
        public static NT_Selected DefaultNode => ForNode(NodeRenderMode.RenderAsCircle);

        /// <summary>
        /// Default edge selection (outlines).
        /// </summary>
        public static NT_Selected DefaultEdge => ForEdge(EdgeRenderMode.RenderOutlines);
    }
}