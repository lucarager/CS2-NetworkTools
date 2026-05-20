namespace NetworkTools.Systems.Tools.Connect {
    using Colossal.Mathematics;

    using Game.Net;
    using Game.Tools;

    using NetworkTools.Systems.Tools.Utils;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    public struct SimpleCurveGenerator : IConnectionGenerator {
        public void InitializeConfig(ref ConnectJobConfig config) {
            var length = math.distance(config.StartPosition, config.EndPosition);
            var dot    = math.dot(config.StartDirection, -config.EndDirection);
            var factor = math.lerp(0.5f, 1f / 3f, math.saturate((dot + 1f) / 2f));

            config.CurveStartPointPosition        = config.StartPosition;
            config.CurveEndPointPosition          = config.EndPosition;
            config.CurveStartControlPointPosition = config.StartPosition + config.StartDirection * (length * factor);
            config.CurveEndControlPointPosition   = config.EndPosition   + config.EndDirection   * (length * factor);
        }

        public void GenerateConnection(
            in  ConnectJobConfig      config,
            ref NativeList<EdgeConfig> curves) {
            var curveBezier = new Bezier4x3 {
                a = config.CurveStartPointPosition,
                b = config.CurveStartControlPointPosition,
                c = config.CurveEndControlPointPosition,
                d = config.CurveEndPointPosition
            };
            curves.Add(new EdgeConfig {
                Bezier = curveBezier,
                Length = MathUtils.Length(curveBezier),
                StartNodeElevation = config.StartElevation,
                EndNodeElevation = config.EndElevation,
                StartNodeFlags = CoursePosFlags.IsFirst | CoursePosFlags.IsRight,
                EndNodeFlags = CoursePosFlags.IsLast | CoursePosFlags.IsRight,
                NetPrefabEntity = config.NetPrefabEntity,
                NetLanePrefabEntity = config.NetLanePrefabEntity,
            });
        }
    }
}
