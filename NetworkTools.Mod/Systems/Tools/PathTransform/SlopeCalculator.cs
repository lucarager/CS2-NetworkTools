// <copyright file="SlopeCalculator.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools.PathTransform {
    #region Using Statements

    using Colossal.Mathematics;

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
            var curvedRatio = config.ApplyCurve(ratio);
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
        public static EdgeHeights CalculateEdgeHeights(
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

            return new EdgeHeights {
                Start     = CalculateHeight(distStart, totalLength, startHeight, deltaHeight, config),
                CtrlStart = CalculateHeight(distCtrlStart, totalLength, startHeight, deltaHeight, config),
                CtrlEnd   = CalculateHeight(distCtrlEnd, totalLength, startHeight, deltaHeight, config),
                End       = CalculateHeight(distEnd, totalLength, startHeight, deltaHeight, config),
            };
        }

        /// <summary>
        /// Applies calculated heights to a bezier curve, accounting for edge direction.
        /// </summary>
        /// <param name="bezier">The bezier curve to modify</param>
        /// <param name="heights">The calculated heights in path order</param>
        /// <param name="isForward">True if edge direction matches path direction</param>
        /// <returns>The modified bezier curve</returns>
        public static Bezier4x3 ApplyHeightsToBezier(in Bezier4x3 bezier, in EdgeHeights heights, bool isForward) {
            var result = bezier;

            if (isForward) {
                result.a.y = heights.Start;
                result.b.y = heights.CtrlStart;
                result.c.y = heights.CtrlEnd;
                result.d.y = heights.End;
            } else {
                result.a.y = heights.End;
                result.b.y = heights.CtrlEnd;
                result.c.y = heights.CtrlStart;
                result.d.y = heights.Start;
            }

            return result;
        }

        /// <summary>
        /// Calculates the heights for all four bezier control points using edge state and context.
        /// Simplified overload that extracts parameters from the state and context structs.
        /// </summary>
        /// <param name="state">The edge transform state containing edge-level data.</param>
        /// <param name="ctx">The transform context containing path-level data.</param>
        /// <returns>Heights for all four control points in path order.</returns>
        public static EdgeHeights CalculateEdgeHeights(in EdgeTransformState state, in TransformContext ctx) {
            return CalculateEdgeHeights(
                state.CumulativeDistance,
                state.Length,
                state.CtrlStartRatio,
                state.CtrlEndRatio,
                ctx.TotalLength,
                ctx.StartHeight,
                ctx.DeltaHeight,
                ctx.Config.Slope);
        }
    }
}
