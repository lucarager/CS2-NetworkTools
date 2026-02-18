// <copyright file="SlopeCurveConfig.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using Unity.Mathematics;

    /// <summary>
    ///     Defines the type of slope curve to apply to road segments.
    /// </summary>
    public enum SlopeTemplate {
        /// <summary>
        ///     Keep existing Y positions (no-op).
        /// </summary>
        Preserve = 0,

        /// <summary>
        ///     Linear interpolation - constant slope throughout the path.
        /// </summary>
        Linear = 1,

        /// <summary>
        ///     Ease-in-out curve - smooth transitions at start and end with steeper middle section.
        /// </summary>
        EaseInOut = 2,

        /// <summary>
        ///     Parabolic curve - creates an arch (hill) or dip (valley) along the path.
        /// </summary>
        Parabolic = 3
    }

    /// <summary>
    ///     Configuration for slope curve application.
    ///     Contains parameters for different curve templates.
    /// </summary>
    public struct SlopeCurveConfig {
        /// <summary>
        ///     The template type to use for slope calculation.
        /// </summary>
        public SlopeTemplate Template;

        // EaseInOut parameters
        /// <summary>
        ///     Length of ease-in transition at start (0 to 0.5).
        ///     Defines how much of the path has gradual slope increase.
        /// </summary>
        public float EaseInLength;

        /// <summary>
        ///     Length of ease-out transition at end (0 to 0.5).
        ///     Defines how much of the path has gradual slope decrease.
        /// </summary>
        public float EaseOutLength;

        // Parabolic parameters
        /// <summary>
        ///     Height of the parabolic arch (-1 to 1).
        ///     Positive creates a hill, negative creates a valley.
        /// </summary>
        public float ArchHeight;

        /// <summary>
        ///     Position of the arch peak/valley along path (0 to 1).
        ///     0.5 places it in the middle.
        /// </summary>
        public float ArchPosition;

        /// <summary>
        ///     Creates a preserve configuration (keeps existing Y positions).
        /// </summary>
        public static SlopeCurveConfig Preserve() {
            return new SlopeCurveConfig {
                Template = SlopeTemplate.Preserve
            };
        }

        /// <summary>
        ///     Creates a linear slope configuration (default).
        /// </summary>
        public static SlopeCurveConfig Linear() {
            return new SlopeCurveConfig {
                Template = SlopeTemplate.Linear
            };
        }

        /// <summary>
        ///     Creates an ease-in-out configuration with specified transition lengths.
        /// </summary>
        /// <param name="inLength">Ease-in length (0 to 0.5)</param>
        /// <param name="outLength">Ease-out length (0 to 0.5)</param>
        public static SlopeCurveConfig EaseInOut(float inLength = 0.25f, float outLength = 0.25f) {
            return new SlopeCurveConfig {
                Template      = SlopeTemplate.EaseInOut,
                EaseInLength  = math.clamp(inLength,  0f, 0.5f),
                EaseOutLength = math.clamp(outLength, 0f, 0.5f)
            };
        }

        /// <summary>
        ///     Creates a parabolic configuration with specified arch properties.
        /// </summary>
        /// <param name="height">Arch height (-1 to 1, negative = valley, positive = hill)</param>
        /// <param name="position">Arch position (0 to 1, 0.5 = middle)</param>
        public static SlopeCurveConfig Parabolic(float height = 0.5f, float position = 0.5f) {
            return new SlopeCurveConfig {
                Template     = SlopeTemplate.Parabolic,
                ArchHeight   = math.clamp(height,   -1f,  1f),
                ArchPosition = math.clamp(position, 0.1f, 0.9f)
            };
        }
    }
}