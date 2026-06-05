namespace NetworkTools.Systems.Tools.Connect {
    using Colossal.Mathematics;

    using Game.Net;
    using Game.Tools;

    using NetworkTools.Systems.Tools.RoadShape;
    using NetworkTools.Systems.Tools.Utils;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    public struct SimpleCurveGenerator : IConnectionGenerator {
        public static void Initialize(NT_ConnectToolSystem tool) {
            var length = math.distance(tool.StartPosition.Value, tool.EndPosition.Value);
            var dot    = math.dot(tool.StartDirection.Value, -tool.EndDirection.Value);
            var factor = math.lerp(0.75f, 0.33f, math.saturate((dot + 1f) / 2f));

            tool.CurveStartPointPosition.Value        = tool.StartPosition.Value;
            tool.CurveEndPointPosition.Value          = tool.EndPosition.Value;
            tool.CurveStartControlPointPosition.Value = tool.StartPosition.Value + tool.StartDirection.Value * (length * factor);
            tool.CurveEndControlPointPosition.Value   = tool.EndPosition.Value   + tool.EndDirection.Value   * (length * factor);
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
                StartNodeElevation = SlopeUtils.ClampElevation(config.StartElevation),
                EndNodeElevation = SlopeUtils.ClampElevation(config.EndElevation),
                StartNodeFlags = CoursePosFlags.IsFirst | CoursePosFlags.IsRight,
                EndNodeFlags = CoursePosFlags.IsLast | CoursePosFlags.IsRight,
                NetPrefabEntity = config.NetPrefabEntity,
                NetLanePrefabEntity = config.NetLanePrefabEntity,
            });
        }
    }
}
