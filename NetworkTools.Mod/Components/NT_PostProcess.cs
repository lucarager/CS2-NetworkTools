// <copyright file="NT_AddNode.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Components {
    using Unity.Entities;

    /// <summary>
    ///     The kind of post-processing operation to perform.
    /// </summary>
    public enum NT_PostProcessOperation {
        DeleteNode,
        DeleteEdge,
        UpdateEdge,
    }

    /// <summary>
    ///     Marks an entity for post-processing after a tool action.
    /// </summary>
    public struct NT_PostProcess : IComponentData {
        public NT_PostProcessOperation Operation;
    }
}
