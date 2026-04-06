// <copyright file="NT_Highlighted.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Components {
    using Unity.Entities;

    /// <summary>
    /// Marks an entity as highlighted with render mode configuration.
    /// </summary>
    public struct NT_Highlighted : IComponentData {
        public NodeRenderMode NodeMode;
        public EdgeRenderMode EdgeMode;

        /// <summary>
        /// Creates a highlight for a node with the specified render mode.
        /// </summary>
        public static NT_Highlighted ForNode(NodeRenderMode mode) => new() { NodeMode = mode, EdgeMode = EdgeRenderMode.None };

        /// <summary>
        /// Creates a highlight for an edge with the specified render mode.
        /// </summary>
        public static NT_Highlighted ForEdge(EdgeRenderMode mode) => new() { NodeMode = NodeRenderMode.None, EdgeMode = mode };

        /// <summary>
        /// Default node highlight (circle).
        /// </summary>
        public static NT_Highlighted DefaultNode => ForNode(NodeRenderMode.RenderAsCircle);

        /// <summary>
        /// Default edge highlight (outlines).
        /// </summary>
        public static NT_Highlighted DefaultEdge => ForEdge(EdgeRenderMode.RenderOutlines);
    }
}
