// <copyright file="ShapeCalculator.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Colossal.Mathematics;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// Burst-compatible utility for shape (XZ) calculations.
    /// Provides methods for calculating and applying XZ transformations to bezier curves.
    /// </summary>
    public static class ShapeCalculator {
        /// <summary>
        /// Calculates XZ position at a given distance along a straight line.
        /// </summary>
        /// <param name="distance">Distance along the path.</param>
        /// <param name="totalLength">Total length of the path.</param>
        /// <param name="startXZ">XZ position at path start.</param>
        /// <param name="endXZ">XZ position at path end.</param>
        /// <returns>Calculated XZ position at the given distance.</returns>
        public static float2 CalculatePositionLinear(
            float  distance,
            float  totalLength,
            float2 startXZ,
            float2 endXZ) {
            var ratio = math.clamp(distance / totalLength, 0f, 1f);
            return math.lerp(startXZ, endXZ, ratio);
        }

        /// <summary>
        /// Calculates the control point ratios for a bezier curve in path order (XZ plane).
        /// </summary>
        /// <param name="bezier">The bezier curve.</param>
        /// <param name="length">The length of the curve.</param>
        /// <param name="isForward">True if edge direction matches path direction.</param>
        /// <param name="controlPointStartRatio">Output: ratio of control point closer to path-start.</param>
        /// <param name="controlPointEndRatio">Output: ratio of control point closer to path-end.</param>
        public static void CalculateControlPointRatios(
            in Bezier4x3 bezier,
            float        length,
            bool         isForward,
            out float    controlPointStartRatio,
            out float    controlPointEndRatio) {
            // Calculate bezier control point ratios based on horizontal distance from 'a'
            var horizontalA = new float2(bezier.a.x, bezier.a.z);
            var horizontalB = new float2(bezier.b.x, bezier.b.z);
            var horizontalC = new float2(bezier.c.x, bezier.c.z);

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
                controlPointStartRatio = bRatio;
                controlPointEndRatio   = cRatio;
            } else {
                controlPointStartRatio = 1f - cRatio;
                controlPointEndRatio   = 1f - bRatio;
            }
        }

        /// <summary>
        /// Calculates XZ positions for all four bezier control points (straighten mode).
        /// </summary>
        /// <param name="cumulativeDistance">Distance along path at the start of this edge.</param>
        /// <param name="edgeLength">Length of this edge.</param>
        /// <param name="controlPointStartRatio">Ratio of control point closer to path-start.</param>
        /// <param name="controlPointEndRatio">Ratio of control point closer to path-end.</param>
        /// <param name="totalLength">Total length of the entire path.</param>
        /// <param name="pathStartXZ">XZ position at path start.</param>
        /// <param name="pathEndXZ">XZ position at path end.</param>
        /// <returns>XZ positions for all four control points in path order.</returns>
        public static EdgePositions CalculateStraightenedPositions(
            float  cumulativeDistance,
            float  edgeLength,
            float  controlPointStartRatio,
            float  controlPointEndRatio,
            float  totalLength,
            float2 pathStartXZ,
            float2 pathEndXZ) {
            var distStart     = cumulativeDistance;
            var distCtrlStart = cumulativeDistance + edgeLength * controlPointStartRatio;
            var distCtrlEnd   = cumulativeDistance + edgeLength * controlPointEndRatio;
            var distEnd       = cumulativeDistance + edgeLength;

            return new EdgePositions {
                Start     = CalculatePositionLinear(distStart, totalLength, pathStartXZ, pathEndXZ),
                ControlPointStart = CalculatePositionLinear(distCtrlStart, totalLength, pathStartXZ, pathEndXZ),
                ControlPointEnd   = CalculatePositionLinear(distCtrlEnd, totalLength, pathStartXZ, pathEndXZ),
                End       = CalculatePositionLinear(distEnd, totalLength, pathStartXZ, pathEndXZ),
            };
        }

        /// <summary>
        /// Calculates XZ positions for all four bezier control points (straighten mode).
        /// Simplified overload that extracts parameters from state and context structs.
        /// </summary>
        /// <param name="state">The edge transform state containing edge-level data.</param>
        /// <param name="ctx">The transform context containing path-level data.</param>
        /// <returns>XZ positions for all four control points in path order.</returns>
        public static EdgePositions CalculateStraightenedPositions(in EdgeTransformState state, in TransformContext ctx) {
            return CalculateStraightenedPositions(
                state.CumulativeDistance,
                state.Length,
                state.ControlPointStartRatio,
                state.ControlPointEndRatio,
                ctx.TotalLength,
                ctx.StartXZ,
                ctx.EndXZ);
        }

        /// <summary>
        /// Applies calculated XZ positions to a bezier curve, preserving Y values.
        /// </summary>
        /// <param name="bezier">The bezier curve to modify.</param>
        /// <param name="positions">The calculated XZ positions in path order.</param>
        /// <param name="isForward">True if edge direction matches path direction.</param>
        /// <returns>The modified bezier curve.</returns>
        public static Bezier4x3 ApplyPositionsToBezier(in Bezier4x3 bezier, in EdgePositions positions, bool isForward) {
            var result = bezier;

            if (isForward) {
                result.a.x = positions.Start.x;     result.a.z = positions.Start.y;
                result.b.x = positions.ControlPointStart.x; result.b.z = positions.ControlPointStart.y;
                result.c.x = positions.ControlPointEnd.x;   result.c.z = positions.ControlPointEnd.y;
                result.d.x = positions.End.x;       result.d.z = positions.End.y;
            } else {
                result.a.x = positions.End.x;       result.a.z = positions.End.y;
                result.b.x = positions.ControlPointEnd.x;   result.b.z = positions.ControlPointEnd.y;
                result.c.x = positions.ControlPointStart.x; result.c.z = positions.ControlPointStart.y;
                result.d.x = positions.Start.x;     result.d.z = positions.Start.y;
            }

            return result;
        }

        /// <summary>
        /// Evaluates a cubic bezier curve at parameter t (0 to 1).
        /// </summary>
        /// <param name="p0">Start point.</param>
        /// <param name="p1">First control point.</param>
        /// <param name="p2">Second control point.</param>
        /// <param name="p3">End point.</param>
        /// <param name="t">Parameter (0 to 1).</param>
        /// <returns>Position on the bezier curve at t.</returns>
        public static float2 EvaluateBezier(float2 p0, float2 p1, float2 p2, float2 p3, float t) {
            var oneMinusT = 1f - t;
            var oneMinusT2 = oneMinusT * oneMinusT;
            var oneMinusT3 = oneMinusT2 * oneMinusT;
            var t2 = t * t;
            var t3 = t2 * t;

            return oneMinusT3 * p0 +
                   3f * oneMinusT2 * t * p1 +
                   3f * oneMinusT * t2 * p2 +
                   t3 * p3;
        }

        /// <summary>
        /// Evaluates the tangent (derivative) of a cubic bezier curve at parameter t.
        /// </summary>
        /// <param name="p0">Start point.</param>
        /// <param name="p1">First control point.</param>
        /// <param name="p2">Second control point.</param>
        /// <param name="p3">End point.</param>
        /// <param name="t">Parameter (0 to 1).</param>
        /// <returns>Tangent vector at t.</returns>
        public static float2 EvaluateBezierTangent(float2 p0, float2 p1, float2 p2, float2 p3, float t) {
            var oneMinusT = 1f - t;
            var oneMinusT2 = oneMinusT * oneMinusT;
            var t2 = t * t;

            return 3f * oneMinusT2 * (p1 - p0) +
                   6f * oneMinusT * t * (p2 - p1) +
                   3f * t2 * (p3 - p2);
        }

        /// <summary>
        /// Calculates XZ positions for all four bezier control points (smooth mode).
        /// Creates a master bezier from path start to end, then samples it for each edge.
        /// </summary>
        /// <param name="cumulativeDistance">Distance along path at the start of this edge.</param>
        /// <param name="edgeLength">Length of this edge.</param>
        /// <param name="controlPointStartRatio">Ratio of control point closer to path-start.</param>
        /// <param name="controlPointEndRatio">Ratio of control point closer to path-end.</param>
        /// <param name="totalLength">Total length of the entire path.</param>
        /// <param name="pathStartXZ">XZ position at path start.</param>
        /// <param name="pathEndXZ">XZ position at path end.</param>
        /// <param name="masterControlPoint1">First control point of master bezier.</param>
        /// <param name="masterControlPoint2">Second control point of master bezier.</param>
        /// <param name="smoothingFactor">How much to smooth (0 = original, 1 = full smooth).</param>
        /// <param name="originalBezier">Original bezier for blending.</param>
        /// <param name="isForward">True if edge direction matches path direction.</param>
        /// <returns>XZ positions for all four control points in path order.</returns>
        public static EdgePositions CalculateSmoothedPositions(
            float     cumulativeDistance,
            float     edgeLength,
            float     controlPointStartRatio,
            float     controlPointEndRatio,
            float     totalLength,
            float2    pathStartXZ,
            float2    pathEndXZ,
            float2    masterControlPoint1,
            float2    masterControlPoint2,
            float     smoothingFactor,
            in Bezier4x3 originalBezier,
            bool      isForward) {
            // Calculate t parameters for each point on this edge
            var tStart     = math.clamp(cumulativeDistance / totalLength, 0f, 1f);
            var tCtrlStart = math.clamp((cumulativeDistance + edgeLength * controlPointStartRatio) / totalLength, 0f, 1f);
            var tCtrlEnd   = math.clamp((cumulativeDistance + edgeLength * controlPointEndRatio) / totalLength, 0f, 1f);
            var tEnd       = math.clamp((cumulativeDistance + edgeLength) / totalLength, 0f, 1f);

            // Sample positions on the master smooth bezier
            var smoothStart     = EvaluateBezier(pathStartXZ, masterControlPoint1, masterControlPoint2, pathEndXZ, tStart);
            var smoothCtrlStart = EvaluateBezier(pathStartXZ, masterControlPoint1, masterControlPoint2, pathEndXZ, tCtrlStart);
            var smoothCtrlEnd   = EvaluateBezier(pathStartXZ, masterControlPoint1, masterControlPoint2, pathEndXZ, tCtrlEnd);
            var smoothEnd       = EvaluateBezier(pathStartXZ, masterControlPoint1, masterControlPoint2, pathEndXZ, tEnd);

            // Get original positions
            float2 origStart, origCtrlStart, origCtrlEnd, origEnd;
            if (isForward) {
                origStart     = new float2(originalBezier.a.x, originalBezier.a.z);
                origCtrlStart = new float2(originalBezier.b.x, originalBezier.b.z);
                origCtrlEnd   = new float2(originalBezier.c.x, originalBezier.c.z);
                origEnd       = new float2(originalBezier.d.x, originalBezier.d.z);
            } else {
                origStart     = new float2(originalBezier.d.x, originalBezier.d.z);
                origCtrlStart = new float2(originalBezier.c.x, originalBezier.c.z);
                origCtrlEnd   = new float2(originalBezier.b.x, originalBezier.b.z);
                origEnd       = new float2(originalBezier.a.x, originalBezier.a.z);
            }

            // Blend between original and smooth positions based on smoothingFactor
            return new EdgePositions {
                Start     = math.lerp(origStart, smoothStart, smoothingFactor),
                ControlPointStart = math.lerp(origCtrlStart, smoothCtrlStart, smoothingFactor),
                ControlPointEnd   = math.lerp(origCtrlEnd, smoothCtrlEnd, smoothingFactor),
                End       = math.lerp(origEnd, smoothEnd, smoothingFactor),
            };
        }

        /// <summary>
        /// Calculates XZ positions for all four bezier control points (smooth mode).
        /// Simplified overload that extracts parameters from state and context structs.
        /// </summary>
        /// <param name="state">The edge transform state containing edge-level data.</param>
        /// <param name="ctx">The transform context containing path-level data.</param>
        /// <param name="masterControlPoint1">First control point of master bezier.</param>
        /// <param name="masterControlPoint2">Second control point of master bezier.</param>
        /// <returns>XZ positions for all four control points in path order.</returns>
        public static EdgePositions CalculateSmoothedPositions(
            in EdgeTransformState state,
            in TransformContext   ctx,
            float2                masterControlPoint1,
            float2                masterControlPoint2) {
            return CalculateSmoothedPositions(
                state.CumulativeDistance,
                state.Length,
                state.ControlPointStartRatio,
                state.ControlPointEndRatio,
                ctx.TotalLength,
                ctx.StartXZ,
                ctx.EndXZ,
                masterControlPoint1,
                masterControlPoint2,
                ctx.Config.Shape.SmoothingFactor,
                state.Bezier,
                state.IsForward);
        }

        /// <summary>
        /// Calculates the control points for a master bezier curve that smoothly connects
        /// the path start to path end. Uses tangent information from edge endpoints.
        /// </summary>
        /// <param name="pathStartXZ">XZ position at path start.</param>
        /// <param name="pathEndXZ">XZ position at path end.</param>
        /// <param name="startTangentXZ">Tangent direction at path start (normalized).</param>
        /// <param name="endTangentXZ">Tangent direction at path end (normalized, pointing into end).</param>
        /// <param name="totalLength">Total length of the path.</param>
        /// <param name="controlPoint1">Output: First control point.</param>
        /// <param name="controlPoint2">Output: Second control point.</param>
        public static void CalculateMasterBezierControls(
            float2    pathStartXZ,
            float2    pathEndXZ,
            float2    startTangentXZ,
            float2    endTangentXZ,
            float     totalLength,
            out float2 controlPoint1,
            out float2 controlPoint2) {
            // Use 1/3 of total length as control point distance for a smooth curve
            var controlDistance = totalLength / 3f;

            // First control point: offset from start in the direction of start tangent
            controlPoint1 = pathStartXZ + math.normalizesafe(startTangentXZ) * controlDistance;

            // Second control point: offset from end in the opposite direction of end tangent
            controlPoint2 = pathEndXZ - math.normalizesafe(endTangentXZ) * controlDistance;
        }

        /// <summary>
        /// Extracts the XZ tangent from a bezier curve at the start or end.
        /// </summary>
        /// <param name="bezier">The bezier curve.</param>
        /// <param name="atStart">True to get start tangent, false for end tangent.</param>
        /// <param name="isForward">True if edge direction matches path direction.</param>
        /// <returns>The XZ tangent vector (not normalized).</returns>
        public static float2 GetBezierTangentXZ(in Bezier4x3 bezier, bool atStart, bool isForward) {
            float2 tangent;
            if (atStart) {
                // Tangent at start is direction from a to b
                if (isForward) {
                    tangent = new float2(bezier.b.x - bezier.a.x, bezier.b.z - bezier.a.z);
                } else {
                    tangent = new float2(bezier.c.x - bezier.d.x, bezier.c.z - bezier.d.z);
                }
            } else {
                // Tangent at end is direction from c to d
                if (isForward) {
                    tangent = new float2(bezier.d.x - bezier.c.x, bezier.d.z - bezier.c.z);
                } else {
                    tangent = new float2(bezier.a.x - bezier.b.x, bezier.a.z - bezier.b.z);
                }
            }
            return tangent;
        }
    }
}
