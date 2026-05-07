namespace NetworkTools.Systems.Tools.Connect {
    using Colossal.Mathematics;

    using Unity.Collections;
    using Unity.Mathematics;

    public struct SimpleCurveGenerator : IConnectionGenerator {
        public void InitializeConfig(ref ConnectJobConfig config) {
            var length = math.distance(config.StartPosition, config.EndPosition);

            config.CurveStartPointPosition        = config.StartPosition;
            config.CurveEndPointPosition          = config.EndPosition;
            config.CurveStartControlPointPosition = config.StartPosition + config.StartDirection * (length / 3);
            config.CurveEndControlPointPosition   = config.EndPosition   + config.EndDirection   * (length / 3);
        }

        public void GenerateConnection(
            in  ConnectJobConfig     config,
            ref NativeList<CurveDef> curves) {
            var curveBezier = new Bezier4x3 {
                a = config.CurveStartPointPosition,
                b = config.CurveStartControlPointPosition,
                c = config.CurveEndControlPointPosition,
                d = config.CurveEndPointPosition
            };
            curves.Add(new CurveDef {
                Bezier = curveBezier,
                Length = MathUtils.Length(curveBezier)
            });
        }
    }
}
