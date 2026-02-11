// <copyright file="SlopeCurveConfig.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
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

        /// <summary>
        ///     Applies the configured curve to a normalized distance value (0 to 1).
        /// </summary>
        /// <param name="t">Normalized distance along path (0 to 1)</param>
        /// <returns>Transformed value based on curve template</returns>
        public float ApplyCurve(float t) {
            return Template switch {
                SlopeTemplate.Preserve => t,
                SlopeTemplate.Linear => t,
                SlopeTemplate.EaseInOut => ApplyEaseInOutCurve(t, EaseInLength, EaseOutLength),
                SlopeTemplate.Parabolic => ApplyParabolicCurve(t, ArchHeight, ArchPosition),
                _ => t
            };
        }

        /// <summary>
        ///     Applies an ease-in-out curve with configurable transition zones.
        ///     Creates smooth transitions at start and end of the slope.
        ///     Uses a sine-based easing for smooth derivative matching.
        /// </summary>
        private static float ApplyEaseInOutCurve(float t, float easeInLength, float easeOutLength) {
            // Handle edge cases
            if (easeInLength < 0.001f && easeOutLength < 0.001f) {
                return t; // Pure linear
            }

            // Clamp to valid range
            t = math.clamp(t, 0f, 1f);

            // Calculate the linear region boundaries
            var linearStart = easeInLength;
            var linearEnd = 1f - easeOutLength;

            // Handle overlapping ease regions (sum > 1)
            if (linearStart >= linearEnd) {
                // Use sine easing for the entire curve (true S-curve)
                // sin goes from 0 to 1 over [0, PI/2], with derivative 0 at ends when mirrored
                var sineT = 0.5f * (1f - math.cos(t * math.PI));
                return sineT;
            }

            // Ease-In Region (0 to easeInLength)
            // Use sine ease-in: starts with derivative 0, ends matching linear slope
            // sin(x * PI/2) for x in [0,1] gives 0 to 1 with derivative PI/2 at x=1
            // We scale to match: output goes from 0 to easeInLength
            if (t < linearStart) {
                var localT = t / easeInLength;
                // Sine ease-in: derivative at end = 1 (matches linear)
                var eased = 1f - math.cos(localT * math.PI * 0.5f);
                return eased * easeInLength;
            }

            // Linear Region (easeInLength to 1-easeOutLength)
            if (t < linearEnd) {
                return t;
            }

            // Ease-Out Region (1-easeOutLength to 1)
            // Use sine ease-out: starts matching linear slope, ends with derivative 0
            var outLocalT = (t - linearEnd) / easeOutLength;
            // Sine ease-out: derivative at start = 1 (matches linear)
            var outEased = math.sin(outLocalT * math.PI * 0.5f);
            return linearEnd + outEased * easeOutLength;
        }

        /// <summary>
        ///     Applies a parabolic curve with configurable arch height and position.
        ///     Endpoints (0 and 1) are preserved; the arch creates a deviation from linear in between.
        /// </summary>
        private static float ApplyParabolicCurve(float t, float archHeight, float archPosition) {
            // Calculate a bump function that is 0 at endpoints and 1 at archPosition
            float bump;

            if (t < archPosition) {
                // Left side of arch: rises from 0 to 1 at archPosition
                var localT = t / archPosition;
                bump = localT * localT;
            }
            else {
                // Right side of arch: falls from 1 at archPosition to 0 at t=1
                var localT = (1f - t) / (1f - archPosition);
                bump = localT * localT;
            }

            // bump is now 0 at endpoints (t=0, t=1), 1 at archPosition

            // Calculate the maximum deviation possible at this t while preserving endpoints
            // At any point t, we can deviate at most min(t, 1-t) to stay in [0, 1]
            // But for arch effect, we want to add to linear based on archHeight

            // archHeight = 0: purely linear (no deviation)
            // archHeight = 1: maximum arch effect (adds bump scaled to reach 1 at peak)
            // archHeight = -1: maximum inverted arch effect (subtracts bump scaled to reach 0 at peak)

            // The deviation at each point: bump * archHeight * scale
            // At archPosition with archHeight=1: we want result=1, so deviation = 1 - archPosition
            // At archPosition with archHeight=-1: we want result=0, so deviation = -archPosition
            var linearValue = t;
            var maxDeviationAtPeak = archHeight >= 0f ? 1f - archPosition : archPosition;
            var deviation = bump * archHeight * maxDeviationAtPeak;

            return linearValue + deviation;
        }
    }
}