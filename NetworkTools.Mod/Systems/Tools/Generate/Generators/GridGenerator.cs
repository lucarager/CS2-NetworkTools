namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Mathematics;
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Base;
    using Unity.Collections;
    using Unity.Mathematics;

    public struct GridGenerator : IGenerator, IHandleableGenerator {
        private const float PreviewDistance = 10f;

        public void InitializeConfig(ref GenerateConfig config) {
        }

        public void GeneratePreview(
            in  float3 StartPosition,
            in  quaternion StartDirection,
            ref NativeList<CurveDef> curves) {
                // Create a rectangle to preview the grid
                var startPos = StartPosition;
                var startDir = math.forward(StartDirection);
                var crossDir = math.cross(startDir, new float3(0, 1, 0));

                var row0col0 = startPos;
                var row0col1 = startPos + crossDir * PreviewDistance;
                var row1col0 = startPos + startDir * PreviewDistance;
                var row1col1 = row0col1 + startDir * PreviewDistance;

                var front = new Bezier4x3(row0col0, row0col0, row0col1, row0col1);
                curves.Add(new CurveDef {
                    Bezier = front,
                    Length = MathUtils.Length(front)
                });

                var back = new Bezier4x3(row1col0, row1col0, row1col1, row1col1);
                curves.Add(new CurveDef {
                    Bezier = back,
                    Length = MathUtils.Length(back)
                });

                var left = new Bezier4x3(row0col0, row0col0 , row1col0, row1col0);
                curves.Add(new CurveDef {
                    Bezier = left,
                    Length = MathUtils.Length(left)
                });

                var right = new Bezier4x3(row0col1, row0col1, row1col1, row1col1);
                curves.Add(new CurveDef {
                    Bezier = right,
                    Length = MathUtils.Length(right)
                });
        }

        public void GenerateNetwork(
            in  GenerateConfig       config,
            ref NativeList<CurveDef> curves) {
        }

        public TransformHandleDefinition[] GetHandleDefinitions(
            in GenerateConfig config) {
            return new[] {
                new TransformHandleDefinition {
                    Key       = HandleKeys.StartPosition,
                    TypeFlags = HandleTypeFlags.Position,
                    Position  = config.StartPosition,
                    Radius    = NT_Handle.PrimaryRadius
                }
            };
        }
    }
}