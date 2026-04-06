// <copyright file="NT_ShapeSlope.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Components.Tools {
    using Unity.Entities;

    /// <summary>
    ///     Component marker for the Shape Slope tool prefab.
    ///     Used by RoadShapeToolSystem to identify slope-specific tool selection.
    /// </summary>
    public struct NT_ShapeSlopeTool : IComponentData { }
}
