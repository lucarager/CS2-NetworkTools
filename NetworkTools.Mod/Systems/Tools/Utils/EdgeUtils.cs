namespace NetworkTools.Systems.Tools.Utils {
    using Colossal.Mathematics;

    using Game.Net;

    using Unity.Entities;
    using Unity.Mathematics;

    internal static class NT_EdgeUtils {
        public enum SplitAction {
            /// <summary>Position is valid for splitting the edge.</summary>
            SplitEdge,

            /// <summary>Position is too close to start - snap to start node instead.</summary>
            SnapToStartNode,

            /// <summary>Position is too close to end - snap to end node instead.</summary>
            SnapToEndNode
        }

        /// <summary>
        ///     Buffer subtracted from length calculations in course processing.
        /// </summary>
        public const float LENGTH_BUFFER = 0.16f;

        /// <summary>
        ///     Safety margin added to minimum calculations.
        /// </summary>
        public const float SAFETY_MARGIN = 1f;

        /// <summary>
        ///     Multiplier applied to minimum split distance when an edge end is connected to other roads.
        /// </summary>
        public const float CONNECTED_END_MULTIPLIER = 2f;

        /// <summary>
        ///     Calculates the minimum split distance (in curve position 0-1) from edge endpoints.
        /// </summary>
        /// <param name="edgeLength">Total length of the edge curve.</param>
        /// <param name="roadWidth">Default width of the road (NetGeometryData.m_DefaultWidth).</param>
        /// <param name="minEdgeLengthRange">
        ///     Minimum edge length from prefab (NetGeometryData.m_EdgeLengthRange.min). Pass 0 if
        ///     unknown.
        /// </param>
        /// <returns>Minimum curve position from endpoints (use both ends: min to 1-min).</returns>
        public static float GetMinimumSplitDistance(float edgeLength, float roadWidth, float minEdgeLengthRange = 0f) {
            if (edgeLength <= 0f) {
                return 0.5f; // Invalid edge, return midpoint
            }

            // Calculate minimum edge segment length
            var halfWidth = roadWidth * 0.5f;
            var minEdgeLength = math.max(halfWidth, minEdgeLengthRange);

            // Add the length buffer used in course processing
            minEdgeLength += LENGTH_BUFFER;

            // Convert to curve position (0-1 range)
            var minCurvePosition = minEdgeLength / edgeLength;

            return math.saturate(minCurvePosition);
        }

        /// <summary>
        ///     Calculates asymmetric minimum and maximum curve positions for splitting,
        ///     accounting for whether each end is connected to other roads.
        ///     Connected ends require a larger minimum distance (approximately double).
        /// </summary>
        /// <param name="edgeLength">Total length of the edge curve.</param>
        /// <param name="roadWidth">Default width of the road (NetGeometryData.m_DefaultWidth).</param>
        /// <param name="minEdgeLengthRange">
        ///     Minimum edge length from prefab (NetGeometryData.m_EdgeLengthRange.min). Pass 0 if unknown.
        /// </param>
        /// <param name="startConnected">True if the start node is connected to other edges.</param>
        /// <param name="endConnected">True if the end node is connected to other edges.</param>
        /// <param name="minCurvePosition">Output: minimum valid curve position (from start).</param>
        /// <param name="maxCurvePosition">Output: maximum valid curve position (from end).</param>
        public static void GetMinMaxSplitPositions(
            float edgeLength,
            float roadWidth,
            float minEdgeLengthRange,
            bool startConnected,
            bool endConnected,
            out float minCurvePosition,
            out float maxCurvePosition) {
            if (edgeLength <= 0f) {
                minCurvePosition = 0.5f;
                maxCurvePosition = 0.5f;
                return;
            }

            var baseMinDistance = GetMinimumSplitDistance(edgeLength, roadWidth, minEdgeLengthRange);

            // Apply multiplier for connected ends
            minCurvePosition = startConnected ? baseMinDistance * CONNECTED_END_MULTIPLIER : baseMinDistance;
            maxCurvePosition = endConnected ? 1f - (baseMinDistance * CONNECTED_END_MULTIPLIER) : 1f - baseMinDistance;

            // Ensure min doesn't exceed max (clamp to midpoint if edge is too short)
            if (minCurvePosition >= maxCurvePosition) {
                minCurvePosition = 0.5f;
                maxCurvePosition = 0.5f;
            }
        }

        /// <summary>
        ///     Calculates a more conservative minimum split distance.
        /// </summary>
        /// <param name="edgeLength">Total length of the edge curve.</param>
        /// <param name="roadWidth">Default width of the road.</param>
        /// <param name="startNodeWidth">Width of the start node composition. Pass 0 if unknown.</param>
        /// <param name="endNodeWidth">Width of the end node composition. Pass 0 if unknown.</param>
        /// <returns>Minimum curve position from endpoints.</returns>
        public static float GetMinimumSplitDistanceConservative(
            float edgeLength,
            float roadWidth,
            float startNodeWidth = 0f,
            float endNodeWidth = 0f) {
            if (edgeLength <= 0f) {
                return 0.5f;
            }

            // Use the road width as base minimum
            var minLength = roadWidth;

            // Consider node composition widths
            minLength = math.max(minLength, startNodeWidth * 0.5f);
            minLength = math.max(minLength, endNodeWidth * 0.5f);

            // Add safety margin
            minLength += SAFETY_MARGIN;

            return math.saturate(minLength / edgeLength);
        }

        /// <summary>
        ///     Clamps a split position to a safe range away from edge endpoints.
        /// </summary>
        /// <param name="curvePosition">The desired split position (0-1).</param>
        /// <param name="edgeLength">Total length of the edge curve.</param>
        /// <param name="roadWidth">Default width of the road.</param>
        /// <param name="minEdgeLengthRange">Minimum edge length from prefab. Pass 0 if unknown.</param>
        /// <returns>Clamped curve position within safe range.</returns>
        public static float ClampSplitPosition(
            float curvePosition,
            float edgeLength,
            float roadWidth,
            float minEdgeLengthRange = 0f) {
            var minDistance = GetMinimumSplitDistance(edgeLength, roadWidth, minEdgeLengthRange);
            return math.clamp(curvePosition, minDistance, 1f - minDistance);
        }

        /// <summary>
        ///     Checks if a split position is valid (far enough from endpoints).
        /// </summary>
        /// <param name="curvePosition">The desired split position (0-1).</param>
        /// <param name="edgeLength">Total length of the edge curve.</param>
        /// <param name="roadWidth">Default width of the road.</param>
        /// <param name="minEdgeLengthRange">Minimum edge length from prefab. Pass 0 if unknown.</param>
        /// <returns>True if the position is valid for splitting.</returns>
        public static bool IsValidSplitPosition(
            float curvePosition,
            float edgeLength,
            float roadWidth,
            float minEdgeLengthRange = 0f) {
            var minDistance = GetMinimumSplitDistance(edgeLength, roadWidth, minEdgeLengthRange);
            return curvePosition >= minDistance && curvePosition <= 1f - minDistance;
        }

        /// <summary>
        ///     Calculates the minimum distance in world units from edge endpoints.
        /// </summary>
        /// <param name="roadWidth">Default width of the road.</param>
        /// <param name="minEdgeLengthRange">Minimum edge length from prefab. Pass 0 if unknown.</param>
        /// <returns>Minimum distance in world units.</returns>
        public static float GetMinimumSplitDistanceWorldUnits(float roadWidth, float minEdgeLengthRange = 0f) {
            var halfWidth = roadWidth * 0.5f;
            var minEdgeLength = math.max(halfWidth, minEdgeLengthRange);
            return minEdgeLength + LENGTH_BUFFER;
        }

        /// <summary>
        ///     Determines what action to take based on split position.
        /// </summary>
        /// <param name="curvePosition">The desired split position (0-1).</param>
        /// <param name="edgeLength">Total length of the edge curve.</param>
        /// <param name="roadWidth">Default width of the road.</param>
        /// <returns>Recommended action for the split.</returns>
        public static SplitAction GetRecommendedAction(
            float curvePosition,
            float edgeLength,
            float roadWidth) {
            var minDistance = GetMinimumSplitDistance(edgeLength, roadWidth);

            if (curvePosition < minDistance) {
                return SplitAction.SnapToStartNode;
            }

            if (curvePosition > 1f - minDistance) {
                return SplitAction.SnapToEndNode;
            }

            return SplitAction.SplitEdge;
        }

        /// <summary>
        ///     Computes a merged bezier curve connecting two neighbor nodes.
        /// </summary>
        public static Bezier4x3 ComputeMergedBezier(Entity nodeEntity,
                                              Edge edge1,
                                              Curve curve1,
                                              Edge edge2,
                                              Curve curve2) {
            // Orient each curve so that b flows away from the node and a flows towards the node. 
            var bezier1 = edge1.m_Start == nodeEntity ? MathUtils.Invert(curve1.m_Bezier) : curve1.m_Bezier;
            var bezier2 = edge2.m_End == nodeEntity ? MathUtils.Invert(curve2.m_Bezier) : curve2.m_Bezier;

            // Tangent Directions
            var tanStart = math.normalize((bezier1.b - bezier1.a));
            var tanEnd = math.normalize((bezier2.c - bezier2.d));

            // Calculate Heuristic Handle Length
            var lengthA = math.distance(bezier1.a, bezier1.b);
            var lengthB = math.distance(bezier2.c, bezier2.d);

            // The new handles should generally be longer to account for the larger span
            // Attempted heuristic is (original_handle_length + distance_between_curves / 2)
            var totalDist = math.distance(bezier1.a, bezier2.d);
            var q1Length = lengthA + (totalDist * 0.1f);
            var q2Length = lengthB + (totalDist * 0.1f);

            // New control points
            var q0 = bezier1.a;
            var q3 = bezier2.d;
            var q1 = q0 + tanStart * q1Length;
            var q2 = q3 + tanEnd * q2Length;

            return new Bezier4x3 { a = q0, b = q1, c = q2, d = q3 };
        }

        public static Bezier4x3 ComputeSimpleMergedBezier(
            Entity nodeEntity,
            Edge edge1,
            Curve curve1,
            Edge edge2,
            Curve curve2) {
            // Orient each curve so .a is at the neighbor end (pointing away from the shared node)
            var b1 = edge1.m_Start == nodeEntity ? MathUtils.Invert(curve1.m_Bezier) : curve1.m_Bezier;
            var b2 = edge2.m_End == nodeEntity ? MathUtils.Invert(curve2.m_Bezier) : curve2.m_Bezier;

            return new Bezier4x3 { a = b1.a, b = b1.b, c = b2.c, d = b2.d };
        }
    }
}