namespace NetworkTools.Systems.Tools.Connect {
    using Colossal.Mathematics;

    using NetworkTools.Systems.Tools.Utils;

    using Unity.Collections;
    using Unity.Mathematics;

    public struct LoopGenerator : IConnectionGenerator {
        /// <summary>
        /// Kappa constant for cubic Bezier approximation of a 90-degree circular arc.
        /// κ = 4(√2 - 1) / 3
        /// </summary>
        private const float Kappa = 0.5522847498f;

        public void InitializeConfig(ref ConnectJobConfig config) {
            var distance = 100f;
            // Place loop center perpendicular to start direction (to the right)
            var right = new float3(config.StartDirection.z, 0f, -config.StartDirection.x);
            config.LoopControlPointPosition = config.StartPosition + config.StartDirection * distance + right * distance;
            config.LoopRadius = distance * 2/3;
        }

        public void GenerateConnection(
            in  ConnectJobConfig      config,
            ref NativeList<EdgeConfig> curves) {
            // We have the following segments:
            // 1: Start node to loop start point, straight segment
            // 2: 3 × 90 degree turn segments forming the loop (270° total)
            // 3: Loop end point to end node, straight segment

            var C = config.LoopControlPointPosition;
            var R = config.LoopRadius;

            // Find loop entry: project center onto the start line, then find closest point on circle
            var tStart          = math.dot(C - config.StartPosition, config.StartDirection);
            var footOnStartLine = config.StartPosition + tStart * config.StartDirection;
            var entryOffset     = footOnStartLine - C;
            entryOffset.y = 0f;
            var dirToEntry             = math.normalizesafe(entryOffset);
            var loopStartPointPosition = new float3(C.x + dirToEntry.x * R, C.y, C.z + dirToEntry.z * R);

            // Determine loop direction (CW vs CCW) based on entry tangent alignment with StartDirection
            var cwTangent = new float3(dirToEntry.z, 0f, -dirToEntry.x);
            var clockwise = math.dot(config.StartDirection, cwTangent) > 0f;

            // Compute loop points: 3 × 90° from entry
            var startAngle = math.atan2(dirToEntry.z, dirToEntry.x);
            var step       = clockwise ? -math.PI * 0.5f : math.PI * 0.5f;

            var midAngle1 = startAngle + step;
            var midAngle2 = startAngle + step * 2f;
            var exitAngle = startAngle + step * 3f;

            var loopMidPoint1Position = new float3(C.x + R * math.cos(midAngle1), C.y, C.z + R * math.sin(midAngle1));
            var loopMidPoint2Position = new float3(C.x + R * math.cos(midAngle2), C.y, C.z + R * math.sin(midAngle2));
            var loopEndPointPosition  = new float3(C.x + R * math.cos(exitAngle), C.y, C.z + R * math.sin(exitAngle));

            // Interpolate Y across the full path for smooth height transitions
            loopStartPointPosition.y = math.lerp(config.StartPosition.y, config.EndPosition.y, 0.2f);
            loopMidPoint1Position.y  = math.lerp(config.StartPosition.y, config.EndPosition.y, 0.4f);
            loopMidPoint2Position.y  = math.lerp(config.StartPosition.y, config.EndPosition.y, 0.6f);
            loopEndPointPosition.y   = math.lerp(config.StartPosition.y, config.EndPosition.y, 0.8f);

            // Compute entry/exit tangent directions for smooth straight segment connections
            var entryTangent = clockwise ? cwTangent : -cwTangent;

            var dirToExit   = new float3(math.cos(exitAngle), 0f, math.sin(exitAngle));
            var exitTangent = clockwise
                ? new float3(dirToExit.z, 0f, -dirToExit.x)
                : new float3(-dirToExit.z, 0f, dirToExit.x);

            // 1: Start → loop start (tangent-smooth straight segment)
            var startSegLen    = math.max(math.length(loopStartPointPosition - config.StartPosition), 0.001f);
            var startTangentLen = startSegLen / 3f;
            var firstBezier = new Bezier4x3 {
                a = config.StartPosition,
                b = config.StartPosition   + config.StartDirection * startTangentLen,
                c = loopStartPointPosition - entryTangent * startTangentLen,
                d = loopStartPointPosition
            };
            curves.Add(new EdgeConfig {
                Bezier = firstBezier,
                Length = MathUtils.Length(firstBezier)
            });

            // 2: The loop itself (3 × 90° arcs)
            var loopBezier1 = Calculate90DegreeCurve(C, loopStartPointPosition, loopMidPoint1Position);
            curves.Add(new EdgeConfig {
                Bezier = loopBezier1,
                Length = MathUtils.Length(loopBezier1)
            });
            var loopBezier2 = Calculate90DegreeCurve(C, loopMidPoint1Position, loopMidPoint2Position);
            curves.Add(new EdgeConfig {
                Bezier = loopBezier2,
                Length = MathUtils.Length(loopBezier2)
            });
            var loopBezier3 = Calculate90DegreeCurve(C, loopMidPoint2Position, loopEndPointPosition);
            curves.Add(new EdgeConfig {
                Bezier = loopBezier3,
                Length = MathUtils.Length(loopBezier3)
            });

            // 3: Loop end → end (tangent-smooth straight segment)
            var endSegLen     = math.max(math.length(config.EndPosition - loopEndPointPosition), 0.001f);
            var endTangentLen = endSegLen / 3f;
            var endBezier = new Bezier4x3 {
                a = loopEndPointPosition,
                b = loopEndPointPosition   + exitTangent          * endTangentLen,
                c = config.EndPosition     + config.EndDirection  * endTangentLen,
                d = config.EndPosition
            };
            curves.Add(new EdgeConfig {
                Bezier = endBezier,
                Length = MathUtils.Length(endBezier)
            });
        }

        /// <summary>
        /// Calculates a cubic Bezier approximating a 90-degree circular arc between two points on a circle.
        /// Uses the standard κ = 4(√2 − 1)/3 approximation for optimal accuracy.
        /// </summary>
        private static Bezier4x3 Calculate90DegreeCurve(float3 center, float3 startPosition, float3 endPosition) {
            var radialStart = startPosition - center;
            var radialEnd   = endPosition   - center;

            // Determine arc direction from the 2D cross product (Y component of 3D cross)
            var crossY = radialStart.x * radialEnd.z - radialStart.z * radialEnd.x;

            float3 tangentStart, tangentEnd;
            if (crossY >= 0f) {
                // CCW: tangent rotated 90° counter-clockwise from radial
                tangentStart = new float3(-radialStart.z, 0f, radialStart.x);
                tangentEnd   = new float3(-radialEnd.z,   0f, radialEnd.x);
            } else {
                // CW: tangent rotated 90° clockwise from radial
                tangentStart = new float3(radialStart.z, 0f, -radialStart.x);
                tangentEnd   = new float3(radialEnd.z,   0f, -radialEnd.x);
            }

            return new Bezier4x3 {
                a = startPosition,
                b = startPosition + Kappa * tangentStart,
                c = endPosition   - Kappa * tangentEnd,
                d = endPosition
            };
        }

    }
}
