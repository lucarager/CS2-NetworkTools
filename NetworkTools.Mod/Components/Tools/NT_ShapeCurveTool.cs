// <copyright file="NT_ShapeCurve.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Components.Tools {
    #region Using Statements

    using Unity.Entities;

    #endregion

    /// <summary>
    ///     Component marker for the Shape Curve tool prefab.
    ///     Used by RoadShapeToolSystem to identify curve-specific tool selection.
    /// </summary>
    public struct NT_ShapeCurveTool : IComponentData { }
}
