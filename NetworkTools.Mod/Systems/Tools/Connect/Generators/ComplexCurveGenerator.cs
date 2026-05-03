namespace NetworkTools.Systems.Tools.Connect {
    using System.Collections.Generic;
    using Colossal.Mathematics;
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Parameters;
    using NetworkTools.Systems.Tools.Base;
    using Unity.Collections;
    using Unity.Mathematics;

    public struct ComplexCurveGenerator : IConnectionGenerator {
        public void InitializeConfig(ref ConnectJobConfig config) {
            // Place curve and control points along each node's outgoing direction
            var distance = 10f;
            config.CurveStartPointPosition        = config.StartPosition + config.StartDirection * distance;
            config.CurveEndPointPosition          = config.EndPosition  + config.EndDirection   * distance;
            config.CurveStartControlPointPosition = config.StartPosition + config.StartDirection * distance * 2;
            config.CurveEndControlPointPosition   = config.EndPosition   + config.EndDirection   * distance * 2;
        }

        public void GenerateConnection(
            in  ConnectJobConfig     config,
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

        /// <summary>
        ///     Builds handle definitions for SimpleCurve mode with parameter references bound directly.
        ///     Resolves parameter refs from the tool's <see cref="NT_BaseToolSystem.ParametersByKey" /> map.
        /// </summary>
        public static TransformHandleDefinition[] BuildHandleDefinitions(
            in ConnectJobConfig config,
            IReadOnlyDictionary<string, ParameterBase> parameters) {
            // Keys used only for parent-child resolution within this definition set
            const int keyStart    = 1;
            const int keyStartCtl = 2;
            const int keyEndCtl   = 3;
            const int keyEnd      = 4;

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
