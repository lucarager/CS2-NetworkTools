namespace NetworkTools.Systems.Tools.Utils {
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
    }
}