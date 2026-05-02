namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Mathematics;
    using Game.Tools;
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Base;
    using NetworkTools.Systems.Tools.Utils;
    using Unity.Collections;
    using Unity.Mathematics;

    public struct GridGenerator : IGenerator, IHandleableGenerator {
        private const float PreviewDistance = 32f;

        public void InitializeConfig(ref GenerateJobConfig config) {
        }

        public void GeneratePreview(
            in  float3                 StartPosition,
            in  quaternion             StartDirection,
            ref NativeList<EdgeConfig> curves) {
            GenerateGrid(StartPosition, StartDirection, PreviewDistance, PreviewDistance, 3, 3, ref curves);
        }

        public void GenerateNetwork(
            in  GenerateJobConfig      config,
            ref NativeList<EdgeConfig> curves) {
            GenerateGrid(config.StartPosition,
                         config.StartDirection,
                         config.GridXSpacing,
                         config.GridZSpacing,
                         config.GridXNum,
                         config.GridZNum,
                         ref curves);
        }

        public void GenerateGrid(
            float3                     startPosition,
            quaternion                 startDirection,
            float                      xSpacing,
            float                      zSpacing,
            int                        xNum,
            int                        zNum,
            ref NativeList<EdgeConfig> curves
        ) {
            var xDir = math.mul(startDirection, new float3(1, 0, 0));
            var zDir = math.mul(startDirection, new float3(0, 0, 1));

            var nodes = new NativeArray<float3>(xNum * zNum, Allocator.Temp);
            for (var j = 0; j < zNum; j++) {
                for (var i = 0; i < xNum; i++) {
                    nodes[j * xNum + i] = startPosition
                                        + xDir * (i * xSpacing)
                                        + zDir * (j * zSpacing);
                }
            }

            for (var row = 0; row < zNum; row++) {
                var isFirstRow = row == 0;
                var isLastRow  = row == zNum - 1;

                for (var col = 0; col < xNum; col++) {
                    var isFirstCol = col == 0;
                    var isLastCol  = col == xNum - 1;

                    // X-direction edge: (row, col) → (row, col+1)
                    if (!isLastCol) {
                        var bezier = NT_EdgeUtils.GenerateStraightEdge(nodes[row * xNum + col],
                                                                       nodes[row * xNum + col + 1]);
                        curves.Add(new EdgeConfig {
                            Bezier         = bezier,
                            Length         = MathUtils.Length(bezier),
                            StartNodeFlags = GridNodeFlags(isFirstRow, isLastRow, isFirstCol, false),
                            EndNodeFlags   = GridNodeFlags(isFirstRow, isLastRow, false,      col + 1 == xNum - 1)
                        });
                    }

                    // Z-direction edge: (row, col) → (row+1, col)
                    if (!isLastRow) {
                        var bezier = NT_EdgeUtils.GenerateStraightEdge(nodes[row       * xNum + col],
                                                                       nodes[(row + 1) * xNum + col]);
                        curves.Add(new EdgeConfig {
                            Bezier         = bezier,
                            Length         = MathUtils.Length(bezier),
                            StartNodeFlags = GridNodeFlags(isFirstRow, false,               isFirstCol, isLastCol),
                            EndNodeFlags   = GridNodeFlags(false,      row + 1 == zNum - 1, isFirstCol, isLastCol)
                        });
                    }
                }
            }

            nodes.Dispose();
        }

        private static CoursePosFlags GridNodeFlags(bool isFirstRow, bool isLastRow, bool isFirstCol, bool isLastCol) {
            var isCorner   = (isFirstRow || isLastRow) && (isFirstCol || isLastCol);
            var isBoundary = isFirstRow || isLastRow || isFirstCol || isLastCol;
            if (isCorner) {
                return CoursePosFlags.IsFirst | CoursePosFlags.IsLast;
            }

            if (isBoundary) {
                return CoursePosFlags.FreeHeight;
            }

            return CoursePosFlags.IsParallel;
        }

        public TransformHandleDefinition[] GetHandleDefinitions(
            in GenerateJobConfig config) {
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
