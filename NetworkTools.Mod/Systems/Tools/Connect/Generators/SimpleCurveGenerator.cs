namespace NetworkTools.Systems.Tools.Connect {
    using Colossal.Mathematics;
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Base;
    using Unity.Collections;
    using Unity.Mathematics;

    public struct SimpleCurveGenerator : IConnectionGenerator, IHandleableConnectionGenerator {
        public void InitializeConfig(in ConnectMode mode, ref ConnectConfig config) {
            // Place curve and control points along each node's outgoing direction
            var distance = 10f;
            config.CurveStartPointPosition        = config.StartPosition + config.StartDirection * distance;
            config.CurveEndPointPosition           = config.EndPosition  + config.EndDirection   * distance;
            config.CurveStartControlPointPosition = config.StartPosition + config.StartDirection * distance * 2;
            config.CurveEndControlPointPosition   = config.EndPosition   + config.EndDirection   * distance * 2;
        }

        public void GenerateConnection(
            in  ConnectMode          mode,
            in  ConnectConfig        config,
            ref NativeList<CurveDef> curves) {
            // We have 3 segments: start node to curve start point, curve start point to curve end point, curve end point to end node

            // First, start -> curve start
            var firstBezier = new Bezier4x3 {
                a = config.StartPosition,
                b = config.StartPosition           + (config.CurveStartPointPosition - config.StartPosition) * 0.5f,
                c = config.CurveStartPointPosition + (config.StartPosition - config.CurveStartPointPosition) * 0.5f,
                d = config.CurveStartPointPosition
            };
            curves.Add(new CurveDef {
                Bezier = firstBezier,
                Length = MathUtils.Length(firstBezier)
            });

            // Next, curve start -> curve end
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

            // Next, curve end -> end
            var endBezier = new Bezier4x3 {
                a = config.CurveEndPointPosition,
                b = config.CurveEndPointPosition + (config.EndPosition           - config.CurveEndPointPosition) * 0.5f,
                c = config.EndPosition           + (config.CurveEndPointPosition - config.EndPosition)           * 0.5f,
                d = config.EndPosition
            };
            curves.Add(new CurveDef {
                Bezier = endBezier,
                Length = MathUtils.Length(endBezier)
            });
        }

        public TransformHandleDefinition[] GetHandleDefinitions(
            in ConnectMode mode,
            in ConnectConfig config) {
            return new[] {
                new TransformHandleDefinition {
                    Key = HandleKeys.CurveStartPointPosition,
                    TypeFlags = HandleTypeFlags.Position,
                    Position = config.CurveStartPointPosition,
                    Radius = NT_Handle.PrimaryRadius
                },
                new TransformHandleDefinition {
                    Key = HandleKeys.CurveStartControlPointPosition,
                    TypeFlags = HandleTypeFlags.Position,
                    Position = config.CurveStartControlPointPosition,
                    ParentKey = HandleKeys.CurveStartPointPosition,
                    Radius = NT_Handle.SecondaryRadius
                },
                new TransformHandleDefinition {
                    Key = HandleKeys.CurveEndControlPointPosition,
                    TypeFlags = HandleTypeFlags.Position,
                    Position = config.CurveEndControlPointPosition,
                    ParentKey = HandleKeys.CurveEndPointPosition,
                    Radius = NT_Handle.SecondaryRadius
                },
                new TransformHandleDefinition {
                    Key = HandleKeys.CurveEndPointPosition,
                    TypeFlags = HandleTypeFlags.Position,
                    Position = config.CurveEndPointPosition,
                    Radius = NT_Handle.PrimaryRadius
                }
            };
        }
    }
}