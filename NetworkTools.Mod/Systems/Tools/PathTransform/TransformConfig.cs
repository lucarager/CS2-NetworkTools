// <copyright file="TransformConfig.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    /// <summary>
    ///     Determines how the transformation job outputs its results.
    /// </summary>
    public enum TransformOutputMode : byte {
        /// <summary>
        ///     Create CreationDefinition + NetCourse entities for preview.
        /// </summary>
        Preview,

        /// <summary>
        ///     Modify existing Curve components and handle intersection adjustments.
        /// </summary>
        Apply
    }

    /// <summary>
    ///     Unified configuration for path transformations.
    ///     Holds both shape (XZ) and slope (Y) settings.
    /// </summary>
    public struct TransformConfig {
        /// <summary>
        ///     Configuration for shape (XZ) transformations.
        /// </summary>
        public ShapeCurveConfig Shape;

        /// <summary>
        ///     Configuration for slope (Y) transformations.
        /// </summary>
        public SlopeCurveConfig Slope;

        /// <summary>
        ///     Whether any transformation will be applied.
        /// </summary>
        public bool HasTransform =>
            Shape.Template != ShapeTemplate.Preserve ||
            Slope.Template != SlopeTemplate.Preserve;

        /// <summary>
        ///     Whether shape transformation is active.
        /// </summary>
        public bool HasShapeTransform => Shape.Template != ShapeTemplate.Preserve;

        /// <summary>
        ///     Whether slope transformation is active.
        /// </summary>
        public bool HasSlopeTransform => Slope.Template != SlopeTemplate.Preserve;

        /// <summary>
        ///     Default config that preserves everything.
        /// </summary>
        public static TransformConfig Preserve() {
            return new TransformConfig {
                Shape = ShapeCurveConfig.Preserve(),
                Slope = SlopeCurveConfig.Preserve()
            };
        }

        /// <summary>
        ///     Creates a slope-only config (preserves shape).
        /// </summary>
        /// <param name="slope">The slope curve configuration.</param>
        public static TransformConfig SlopeOnly(SlopeCurveConfig slope) {
            return new TransformConfig {
                Shape = ShapeCurveConfig.Preserve(),
                Slope = slope
            };
        }

        /// <summary>
        ///     Creates a shape-only config (preserves slope).
        /// </summary>
        /// <param name="shape">The shape curve configuration.</param>
        public static TransformConfig ShapeOnly(ShapeCurveConfig shape) {
            return new TransformConfig {
                Shape = shape,
                Slope = SlopeCurveConfig.Preserve()
            };
        }

        /// <summary>
        ///     Creates a combined config with both shape and slope transforms.
        /// </summary>
        /// <param name="shape">The shape curve configuration.</param>
        /// <param name="slope">The slope curve configuration.</param>
        public static TransformConfig Combined(ShapeCurveConfig shape, SlopeCurveConfig slope) {
            return new TransformConfig {
                Shape = shape,
                Slope = slope
            };
        }
    }
}