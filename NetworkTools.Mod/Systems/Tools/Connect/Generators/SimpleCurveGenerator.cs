namespace NetworkTools.Systems.Tools.Connect {
    using System.Collections.Generic;

    using Colossal.Mathematics;

    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Base;
    using NetworkTools.Systems.Tools.Parameters;

    using Unity.Collections;
    using Unity.Mathematics;

    public struct SimpleCurveGenerator : IConnectionGenerator {
        public void InitializeConfig(ref ConnectJobConfig config) {
            // Place curve and control points along each node's outgoing direction
            var length = math.distance(config.StartPosition, config.EndPosition);

            config.CurveStartPointPosition        = config.StartPosition;
            config.CurveEndPointPosition          = config.EndPosition;
            config.CurveStartControlPointPosition = config.StartPosition + config.StartDirection * (length / 3);
            config.CurveEndControlPointPosition   = config.EndPosition   + config.EndDirection   * (length / 3);
        }

        public void GenerateConnection(
            in  ConnectJobConfig     config,
            ref NativeList<CurveDef> curves) {
            // Curve start -> curve end
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

        /// <summary>
        ///     Builds handle definitions for SimpleCurve mode with parameter references bound directly.
        ///     Resolves parameter refs from the tool's <see cref="NT_BaseToolSystem.ParametersByKey" /> map.
        /// </summary>
        public static TransformHandleDefinition[] BuildHandleDefinitions(
            in ConnectJobConfig config,
            IReadOnlyDictionary<string, ParameterBase> parameters) {
            // Keys used only for parent-child resolution within this definition set
            const int keyStart = 1;
            const int keyStartCtl = 2;
            const int keyEndCtl = 3;
            const int keyEnd = 4;

            return new[] {
                new TransformHandleDefinition {
                    Key       = keyStart,
                    TypeFlags = HandleTypeFlags.Position,
                    Position  = config.CurveStartPointPosition,
                    Radius    = NT_Handle.PrimaryRadius,
                    Parameter = parameters["connect.curveStartPointPosition"]
                },
                new TransformHandleDefinition {
                    Key       = keyStartCtl,
                    TypeFlags = HandleTypeFlags.Position,
                    Position  = config.CurveStartControlPointPosition,
                    ParentKey = keyStart,
                    Radius    = NT_Handle.SecondaryRadius,
                    Parameter = parameters["connect.curveStartControlPointPosition"]
                },
                new TransformHandleDefinition {
                    Key       = keyEndCtl,
                    TypeFlags = HandleTypeFlags.Position,
                    Position  = config.CurveEndControlPointPosition,
                    ParentKey = keyEnd,
                    Radius    = NT_Handle.SecondaryRadius,
                    Parameter = parameters["connect.curveEndControlPointPosition"]
                },
                new TransformHandleDefinition {
                    Key       = keyEnd,
                    TypeFlags = HandleTypeFlags.Position,
                    Position  = config.CurveEndPointPosition,
                    Radius    = NT_Handle.PrimaryRadius,
                    Parameter = parameters["connect.curveEndPointPosition"]
                }
            };
        }
    }
}
