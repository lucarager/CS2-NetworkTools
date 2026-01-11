// <copyright file="SlopeCurveConfig.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools {
    using Unity.Mathematics;

    /// <summary>
    /// Defines the type of slope curve to apply to road segments.
    /// </summary>
    public enum SlopeTemplate {
        /// <summary>
        /// Linear interpolation - constant slope throughout the path.
        /// </summary>
        Linear = 0,

        /// <summary>
        /// Ease-in-out curve - smooth transitions at start and end with steeper middle section.
        /// </summary>
        EaseInOut = 1,

        /// <summary>
        /// Parabolic curve - creates an arch (hill) or dip (valley) along the path.
        /// </summary>
        Parabolic = 2,
    }

    /// <summary>
    /// Configuration for slope curve application.
    /// Contains parameters for different curve templates.
    /// </summary>
    public struct SlopeCurveConfig {
        /// <summary>
        /// The template type to use for slope calculation.
        /// </summary>
        public SlopeTemplate Template;

        // EaseInOut parameters
        /// <summary>
        /// Length of ease-in transition at start (0 to 0.5).
        /// Defines how much of the path has gradual slope increase.
        /// </summary>
        public float EaseInLength;

        /// <summary>
        /// Length of ease-out transition at end (0 to 0.5).
        /// Defines how much of the path has gradual slope decrease.
        /// </summary>
        public float EaseOutLength;

        // Parabolic parameters
        /// <summary>
        /// Height of the parabolic arch (-1 to 1).
        /// Positive creates a hill, negative creates a valley.
        /// </summary>
        public float ArchHeight;

        /// <summary>
        /// Position of the arch peak/valley along path (0 to 1).
        /// 0.5 places it in the middle.
        /// </summary>
        public float ArchPosition;

        /// <summary>
        /// Creates a linear slope configuration (default).
        /// </summary>
        public static SlopeCurveConfig Linear() => new SlopeCurveConfig {
            Template = SlopeTemplate.Linear
        };

        /// <summary>
        /// Creates an ease-in-out configuration with specified transition lengths.
        /// </summary>
        /// <param name="inLength">Ease-in length (0 to 0.5)</param>
        /// <param name="outLength">Ease-out length (0 to 0.5)</param>
        public static SlopeCurveConfig EaseInOut(float inLength = 0.25f, float outLength = 0.25f) => new SlopeCurveConfig {
            Template = SlopeTemplate.EaseInOut,
            EaseInLength = math.clamp(inLength, 0f, 0.5f),
            EaseOutLength = math.clamp(outLength, 0f, 0.5f)
        };

        /// <summary>
        /// Creates a parabolic configuration with specified arch properties.
        /// </summary>
        /// <param name="height">Arch height (-1 to 1, negative = valley, positive = hill)</param>
        /// <param name="position">Arch position (0 to 1, 0.5 = middle)</param>
        public static SlopeCurveConfig Parabolic(float height = 0.5f, float position = 0.5f) => new SlopeCurveConfig {
            Template = SlopeTemplate.Parabolic,
            ArchHeight = math.clamp(height, -1f, 1f),
            ArchPosition = math.clamp(position, 0.1f, 0.9f)
        };

        /// <summary>
        /// Applies the configured curve to a normalized distance value (0 to 1).
        /// </summary>
        /// <param name="t">Normalized distance along path (0 to 1)</param>
        /// <returns>Transformed value based on curve template</returns>
        public float ApplyCurve(float t) {
            return Template switch {
                SlopeTemplate.Linear => t,
                SlopeTemplate.EaseInOut => ApplyEaseInOutCurve(t, EaseInLength, EaseOutLength),
                SlopeTemplate.Parabolic => ApplyParabolicCurve(t, ArchHeight, ArchPosition),
                _ => t
            };
        }

        /// <summary>
        /// Applies an ease-in-out curve with configurable transition zones.
        /// </summary>
        private static float ApplyEaseInOutCurve(float t, float easeInLength, float easeOutLength) {
            float linearStart = easeInLength;
            float linearEnd = 1f - easeOutLength;

            // Ease-In Region (0 to easeInLength)
            if (t < linearStart && easeInLength > 0.001f) {
                float localT = t / easeInLength;
                float eased = localT * localT * localT; // Cubic ease-in for stronger effect
                return eased * easeInLength;
            }

            // Linear Region (easeInLength to 1-easeOutLength)
            if (t < linearEnd) {
                return t;
            }

            // Ease-Out Region (1-easeOutLength to 1)
            if (easeOutLength > 0.001f) {
                float localT = (t - linearEnd) / easeOutLength;
                float eased = 1f - math.pow(1f - localT, 3f); // Cubic ease-out for stronger effect
                return linearEnd + (eased * easeOutLength);
            }

            return t;
        }

        /// <summary>
        /// Applies a parabolic curve with configurable arch height and position.
        /// </summary>
        private static float ApplyParabolicCurve(float t, float archHeight, float archPosition) {
            // Calculate parabola with peak/valley at archPosition
            float parabola;

            if (t < archPosition) {
                // Left side of arch: rises from low point to peak
                float localT = t / archPosition;
                parabola = localT * localT;
            } else {
                // Right side of arch: falls from peak to low point
                float localT = (1f - t) / (1f - archPosition);
                parabola = localT * localT;
            }

            // parabola is now 0 at endpoints, 1 at archPosition (bridge shape)

            // Mix linear with parabolic based on archHeight
            // archHeight = 0: purely linear
            // archHeight = 1: maximum arch effect (bridge)
            // archHeight = -1: maximum inverted arch effect (valley)
            if (archHeight >= 0f) {
                // Bridge: blend towards parabola (endpoints low, middle high)
                return math.lerp(t, parabola, archHeight);
            } else {
                // Valley: invert parabola (endpoints high, middle low)
                return math.lerp(t, 1f - parabola, -archHeight);
            }
        }
    }
}
