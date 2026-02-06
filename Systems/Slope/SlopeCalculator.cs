// <copyright file="SlopeCalculator.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using Colossal.Mathematics;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// Per-edge metadata for slope calculations.
    /// Control point ratios are stored in PATH order (not bezier order).
    /// </summary>
    public struct EdgeSlopeData {
        public float Length;
        public float CtrlStartRatio;  // Path-ordered: ratio of control point closer to path-start
        public float CtrlEndRatio;    // Path-ordered: ratio of control point closer to path-end
        public bool  IsForward;       // True if edge direction matches path direction
        public float OldHeight;       // Original height at path-end of this segment (for intersection updates)
    }

    /// <summary>
    /// Pre-calculated heights for an edge's control points in path order.
    /// </summary>
    public struct EdgeHeights {
        public float Start;      // Height at path-start of segment
        public float CtrlStart;  // Height at control point closer to path-start
        public float CtrlEnd;    // Height at control point closer to path-end
        public float End;        // Height at path-end of segment
    }

    /// <summary>
    /// Burst-compatible utility struct for slope calculations.
    /// Contains static methods for calculating edge metadata and applying height transformations.
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
        /// Calculates the control point ratios for a bezier curve in path order.
        /// </summary>
        /// <param name="bezier">The bezier curve</param>
        /// <param name="length">The length of the curve</param>
        /// <param name="isForward">True if edge direction matches path direction</param>
        /// <param name="ctrlStartRatio">Output: ratio of control point closer to path-start</param>
        /// <param name="ctrlEndRatio">Output: ratio of control point closer to path-end</param>
        public static void CalculateControlPointRatios(
            in Bezier4x3 bezier,
            float        length,
            bool         isForward,
            out float    ctrlStartRatio,
            out float    ctrlEndRatio) {
            // Calculate bezier control point ratios based on horizontal distance from 'a'
            var horizontalA = new float3(bezier.a.x, 0f, bezier.a.z);
            var horizontalB = new float3(bezier.b.x, 0f, bezier.b.z);
            var horizontalC = new float3(bezier.c.x, 0f, bezier.c.z);

            float bRatio, cRatio;
            if (length > 0.01f) {
                bRatio = math.clamp(math.distance(horizontalA, horizontalB) / length, 0f, 1f);
                cRatio = math.clamp(math.distance(horizontalA, horizontalC) / length, 0f, 1f);
            } else {
                bRatio = 1f / 3f;
                cRatio = 2f / 3f;
            }

            // Convert bezier ratios to path-ordered ratios
            // Forward: B is closer to path-start, C is closer to path-end
            // Reversed: C is closer to path-start, B is closer to path-end
            if (isForward) {
                ctrlStartRatio = bRatio;
                ctrlEndRatio   = cRatio;
            } else {
                ctrlStartRatio = 1f - cRatio;
                ctrlEndRatio   = 1f - bRatio;
            }
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
    }
}
