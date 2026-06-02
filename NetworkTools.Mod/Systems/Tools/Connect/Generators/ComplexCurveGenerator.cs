namespace NetworkTools.Systems.Tools.Connect {
    using Colossal.Mathematics;

    using Game.Net;
    using Game.Tools;

    using NetworkTools.Systems.Tools.RoadShape;
    using NetworkTools.Systems.Tools.Utils;

    using Unity.Collections;
    using Unity.Mathematics;

    public struct ComplexCurveGenerator : IConnectionGenerator {
        public void InitializeConfig(ref ConnectJobConfig config) {
            var length = math.distance(config.StartPosition, config.EndPosition);
            var dot    = math.dot(config.StartDirection, -config.EndDirection);
            var factor = math.lerp(0.75f, 0.33f, math.saturate((dot + 1f) / 2f));

            config.ComplexStartPointPosition        = config.StartPosition;
            config.ComplexEndPointPosition          = config.EndPosition;
            config.ComplexStartControlPointPosition = config.StartPosition + config.StartDirection * (length * factor);
            config.ComplexEndControlPointPosition   = config.EndPosition   + config.EndDirection   * (length * factor);

            var simpleBezier = new Bezier4x3 {
                a = config.ComplexStartPointPosition,
                b = config.ComplexStartControlPointPosition,
                c = config.ComplexEndControlPointPosition,
                d = config.ComplexEndPointPosition
            };
            config.ComplexMidPosition = MathUtils.Position(simpleBezier, 0.5f);
            config.ComplexMidRotation = math.normalizesafe(MathUtils.Tangent(simpleBezier, 0.5f));
        }

        public void GenerateConnection(
            in  ConnectJobConfig      config,
            ref NativeList<EdgeConfig> curves) {
            var midPos = config.ComplexMidPosition;
            var midDir = math.normalizesafe(config.ComplexMidRotation);

            var distToStart = math.distance(midPos, config.ComplexStartPointPosition);
            var distToEnd   = math.distance(midPos, config.ComplexEndPointPosition);

            const float kInnerFactor = 1f / 3f;
            var innerCP1 = midPos - midDir * (distToStart * kInnerFactor);
            var innerCP2 = midPos + midDir * (distToEnd   * kInnerFactor);

            var bezier1 = new Bezier4x3 {
                a = config.ComplexStartPointPosition,
                b = config.ComplexStartControlPointPosition,
                c = innerCP1,
                d = midPos
            };
            curves.Add(new EdgeConfig {
                Bezier             = bezier1,
                Length             = MathUtils.Length(bezier1),
                StartNodeElevation = SlopeUtils.ClampElevation(config.StartElevation),
                EndNodeElevation   = 0f,
                StartNodeFlags     = CoursePosFlags.IsFirst | CoursePosFlags.IsRight,
                EndNodeFlags       = CoursePosFlags.IsRight,
                NetPrefabEntity     = config.NetPrefabEntity,
                NetLanePrefabEntity = config.NetLanePrefabEntity,
            });

            var bezier2 = new Bezier4x3 {
                a = midPos,
                b = innerCP2,
                c = config.ComplexEndControlPointPosition,
                d = config.ComplexEndPointPosition
            };
            curves.Add(new EdgeConfig {
                Bezier             = bezier2,
                Length             = MathUtils.Length(bezier2),
                StartNodeElevation = 0f,
                EndNodeElevation   = SlopeUtils.ClampElevation(config.EndElevation),
                StartNodeFlags     = CoursePosFlags.IsRight,
                EndNodeFlags       = CoursePosFlags.IsLast | CoursePosFlags.IsRight,
                NetPrefabEntity     = config.NetPrefabEntity,
                NetLanePrefabEntity = config.NetLanePrefabEntity,
            });
        }
    }
}
