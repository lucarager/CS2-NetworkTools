namespace NetworkTools.Systems.Tools.RoadShape {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Colossal.Mathematics;

    using Unity.Mathematics;

    public static class SlopeUtils {
        /// <summary>
        ///     Height thresholds that forces a road to be a tunnel
        /// </summary>
        public static float2 TunnelThreshold = new(-12f, -12f);

        /// <summary>
        ///     Height thresholds that forces a road to be elevated. 
        /// </summary>
        public static float2 ElevatedThreshold = new(8f, 8f);

        /// <summary>
        ///     Height that places road on at ground level.
        /// </summary>
        public static float2 ForceGroundElevation = new(0f, 0f);

        /// <summary>
        ///     Clamps an elevation value to the appropriate threshold so the game
        ///     treats the node as free-height rather than terrain-clinging.
        /// </summary>
        public static float ClampElevation(float elevation) {
            return elevation >= 0f ? ElevatedThreshold.x : TunnelThreshold.x;
        }

        /// <summary>
        /// Gets the height at a given bezier parameter t.
        /// Use this for uniformly-parameterized curves (like linear slopes).
        /// </summary>
        public static float GetHeightAtCurvePosition(Bezier4x3 curve, float t) {
            return MathUtils.Position(curve, t).y;
        }

        /// <summary>
        /// Gets the height from a 2D height curve where x represents path ratio (0-1) and y represents height.
        /// Solves for the bezier parameter t where x(t) = pathRatio, then returns y(t).
        /// Use this for non-uniformly parameterized curves (like ease-in-out).
        /// </summary>
        /// <param name="curve">A bezier where x = normalized path position (0-1), y = height</param>
        /// <param name="pathRatio">The desired path ratio (0-1)</param>
        /// <returns>The height at the given path ratio</returns>
        public static float GetHeightAtPathRatio(Bezier4x3 curve, float pathRatio) {
            var t = FindParameterForPathRatio(curve, pathRatio);
            return MathUtils.Position(curve, t).y;
        }

        /// <summary>
        /// Gets the slope (dy/dx) from a 2D height curve at a given path ratio.
        /// Use this to ensure tangent continuity at shared nodes.
        /// </summary>
        /// <param name="curve">A bezier where x = normalized path position (0-1), y = height</param>
        /// <param name="pathRatio">The desired path ratio (0-1)</param>
        /// <returns>The slope (rise/run) at the given path ratio</returns>
        public static float GetSlopeAtPathRatio(Bezier4x3 curve, float pathRatio) {
            var t = FindParameterForPathRatio(curve, pathRatio);
            var tangent = MathUtils.Tangent(curve, t);

            // Avoid division by zero - return 0 slope if tangent.x is near zero
            if (math.abs(tangent.x) < 0.0001f) {
                return 0f;
            }

            return tangent.y / tangent.x;
        }

        /// <summary>
        /// Finds the bezier parameter t where x(t) = pathRatio using Newton-Raphson iteration.
        /// </summary>
        private static float FindParameterForPathRatio(Bezier4x3 curve, float pathRatio) {
            var t = pathRatio; // Initial guess

            // Newton-Raphson: t_new = t - f(t)/f'(t) where f(t) = x(t) - pathRatio
            for (var i = 0; i < 8; i++) {
                var pos = MathUtils.Position(curve, t);
                var tangent = MathUtils.Tangent(curve, t);

                var error = pos.x - pathRatio;
                if (math.abs(error) < 0.0001f) {
                    break;
                }

                // Avoid division by zero
                if (math.abs(tangent.x) < 0.0001f) {
                    break;
                }

                t -= error / tangent.x;
                t = math.clamp(t, 0f, 1f);
            }

            return t;
        }

        /// <summary>
        /// Applies calculated heights to a bezier curve, accounting for edge direction.
        /// </summary>
        /// <param name="bezier">The bezier curve to modify</param>
        /// <param name="startHeight">Height at path-start of segment</param>
        /// <param name="ctrlStartHeight">Height at control point closer to path-start</param>
        /// <param name="ctrlEndHeight">Height at control point closer to path-end</param>
        /// <param name="endHeight">Height at path-end of segment</param>
        /// <param name="isForward">True if edge direction matches path direction</param>
        /// <returns>The modified bezier curve</returns>
        public static Bezier4x3 ApplyHeightsToBezier(
            in Bezier4x3 bezier,
            float startHeight,
            float ctrlStartHeight,
            float ctrlEndHeight,
            float endHeight,
            bool isForward) {
            var result = bezier;

            if (isForward)
            {
                result.a.y = startHeight;
                result.b.y = ctrlStartHeight;
                result.c.y = ctrlEndHeight;
                result.d.y = endHeight;
            } else
            {
                result.a.y = endHeight;
                result.b.y = ctrlEndHeight;
                result.c.y = ctrlStartHeight;
                result.d.y = startHeight;
            }

            return result;
        }
    }
}
