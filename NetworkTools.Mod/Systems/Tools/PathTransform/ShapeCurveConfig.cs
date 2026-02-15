// <copyright file="ShapeCurveConfig.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools.PathTransform {
    #region Using Statements

    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// Defines the type of shape curve to apply to road segments (XZ plane).
    /// </summary>
    public enum ShapeTemplate {
        /// <summary>
        /// Keep existing XZ positions (no-op).
        /// </summary>
        Preserve = 0,

        /// <summary>
        /// Align all nodes along a straight line between start/end.
        /// </summary>
        Straighten = 1,

        /// <summary>
        /// Fit nodes to a smooth bezier curve.
        /// </summary>
        Smooth = 2,

        /// <summary>
        /// Redistribute nodes evenly along the path.
        /// </summary>
        EqualSpacing = 3,
    }

    /// <summary>
    /// Configuration for shape curve application.
    /// </summary>
    public struct ShapeCurveConfig {
        /// <summary>
        /// The template type to use for shape calculation.
        /// </summary>
        public ShapeTemplate Template;

        /// <summary>
        /// Smoothing factor (0-1), how much to smooth.
        /// Used with Smooth template.
        /// </summary>
        public float SmoothingFactor;

        /// <summary>
        /// Creates a preserve configuration (keeps existing XZ positions).
        /// </summary>
        public static ShapeCurveConfig Preserve() => new ShapeCurveConfig {
            Template = ShapeTemplate.Preserve,
        };

        /// <summary>
        /// Creates a straighten configuration.
        /// </summary>
        public static ShapeCurveConfig Straighten() => new ShapeCurveConfig {
            Template = ShapeTemplate.Straighten,
        };

        /// <summary>
        /// Creates a smooth configuration with the specified smoothing factor.
        /// </summary>
        /// <param name="factor">Smoothing factor (0 to 1).</param>
        public static ShapeCurveConfig Smooth(float factor = 0.5f) => new ShapeCurveConfig {
            Template = ShapeTemplate.Smooth,
            SmoothingFactor = math.clamp(factor, 0f, 1f),
        };

        /// <summary>
        /// Creates an equal spacing configuration.
        /// </summary>
        public static ShapeCurveConfig EqualSpacing() => new ShapeCurveConfig {
            Template = ShapeTemplate.EqualSpacing,
        };
    }
}
