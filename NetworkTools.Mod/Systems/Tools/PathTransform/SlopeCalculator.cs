// <copyright file="SlopeCalculator.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Colossal.Mathematics;
    using NetworkTools.Systems.Tools.RoadShape;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// Burst-compatible utility class for slope (height/Y) calculations.
    /// Contains static methods for calculating edge heights and applying height transformations.
    /// </summary>
    public static class SlopeCalculator {
        /// <summary>
        /// Calculates height at a given distance along the path using the curve config.
        /// </summary>
        /// <param name="distance">Distance along the path</param>
        /// <param name="totalLength">Total length of the path</param>
        /// <param name="startHeight">Height at path start</param>
        /// <param name="deltaHeight">Height difference (end - start)</param>
        /// <param name="config">Curve configuration</param>
        /// <returns>Calculated height at the given distance</returns>
        public static float CalculateHeight(float distance, float totalLength, float startHeight, float deltaHeight, SlopeCurveConfig config) {
            var ratio       = distance / totalLength;
            var curvedRatio = ApplyCurve(ratio, in config);
            return startHeight + deltaHeight * curvedRatio;
        }

        /// <summary>
        /// Calculates the heights for all four bezier control points based on path position.
        /// </summary>
        /// <param name="cumulativeDistance">Distance along path at the start of this edge</param>
        /// <param name="edgeLength">Length of this edge</param>
        /// <param name="ctrlStartRatio">Ratio of control point closer to path-start</param>
        /// <param name="ctrlEndRatio">Ratio of control point closer to path-end</param>
        /// <param name="totalLength">Total length of the entire path</param>
        /// <param name="startHeight">Height at path start</param>
        /// <param name="deltaHeight">Height difference (end - start)</param>
        /// <param name="config">Curve configuration</param>
        /// <returns>Heights for all four control points in path order</returns>
        public static EdgeControlPointHeights CalculateEdgeHeights(
            float           cumulativeDistance,
            float           edgeLength,
            float           ctrlStartRatio,
            float           ctrlEndRatio,
            float           totalLength,
            float           startHeight,
            float           deltaHeight,
            SlopeCurveConfig config) {
            // Calculate distances in path order (direction-independent)
            var distStart     = cumulativeDistance;
            var distCtrlStart = cumulativeDistance + edgeLength * ctrlStartRatio;
            var distCtrlEnd   = cumulativeDistance + edgeLength * ctrlEndRatio;
            var distEnd       = cumulativeDistance + edgeLength;

            return new EdgeControlPointHeights {
                Start     = CalculateHeight(distStart, totalLength, startHeight, deltaHeight, config),
                CtrlStart = CalculateHeight(distCtrlStart, totalLength, startHeight, deltaHeight, config),
                CtrlEnd   = CalculateHeight(distCtrlEnd, totalLength, startHeight, deltaHeight, config),
                End       = CalculateHeight(distEnd, totalLength, startHeight, deltaHeight, config),
            };
        }

        /// <summary>
        /// Calculates the heights for all four bezier control points using edge state and context.
        /// Simplified overload that extracts parameters from the state and context structs.
        /// </summary>
        /// <param name="state">The edge transform state containing edge-level data.</param>
        /// <param name="ctx">The transform context containing path-level data.</param>
        /// <returns>Heights for all four control points in path order.</returns>
        public static EdgeControlPointHeights CalculateEdgeHeights(in EdgeTransformState state, in TransformContext ctx) {
            return CalculateEdgeHeights(
                state.CumulativeDistance,
                state.Length,
                state.ControlPointStartRatio,
                state.ControlPointEndRatio,
                ctx.TotalLength,
                ctx.StartHeight,
                ctx.DeltaHeight,
                ctx.Config.Slope);
        }

        // ========================================
        // Curve Functions
        // ========================================

        /// <summary>
        /// Applies the configured curve to a normalized distance value (0 to 1).
        /// </summary>
        /// <param name="t">Normalized distance along path (0 to 1)</param>
        /// <param name="config">The slope curve configuration</param>
        /// <returns>Transformed value based on curve template</returns>
        public static float ApplyCurve(float t, in SlopeCurveConfig config) {
            return config.Template switch {
                SlopeTemplate.Preserve => t,
                SlopeTemplate.Linear => t,
                SlopeTemplate.EaseInOut => ApplyEaseInOutCurve(t, config.EaseInLength, config.EaseOutLength),
                SlopeTemplate.Parabolic => ApplyParabolicCurve(t, config.ArchHeight, config.ArchPosition),
                _ => t
            };
        }

        /// <summary>
        /// Applies an ease-in-out curve with configurable transition zones.
        /// Creates smooth transitions at start and end of the slope.
        /// Uses a sine-based easing for smooth derivative matching.
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
        /// Applies a parabolic curve with configurable arch height and position.
        /// Endpoints (0 and 1) are preserved; the arch creates a deviation from linear in between.
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
