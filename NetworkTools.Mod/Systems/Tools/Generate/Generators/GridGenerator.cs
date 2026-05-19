namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Mathematics;

    using Game.Tools;

    using NetworkTools.Systems.Tools.Utils;

    using Unity.Collections;
    using Unity.Mathematics;

    public struct GridGenerator : IGenerator {
        public void InitializeConfig(ref GenerateJobConfig config) {
        }

        public void Generate(
            in  GenerateJobConfig      config,
            float                      netWidth,
            float                      elevationLimit,
            ref NativeList<EdgeConfig> curves) {
            // User-facing spacing is edge-to-edge; add the prefab width to get
            // the centerline-to-centerline distance the geometry needs.
            var yOffset = config.Elevation + config.BaselineElevation;
            var freeHeight = yOffset > -elevationLimit && yOffset < elevationLimit
                ? CoursePosFlags.FreeHeight
                : (CoursePosFlags)0;
            GenerateGrid(config.Position + new float3(0, yOffset, 0),
                         config.StartDirection,
                         config.GridXSpacing + netWidth,
                         config.GridZSpacing + netWidth,
                         config.GridXNum + 1,
                         config.GridZNum + 1,
                         freeHeight,
                         ref curves);
        }

        public void GenerateGrid(
            float3                     startPosition,
            quaternion                 startDirection,
            float                      xSpacing,
            float                      zSpacing,
            int                        xNum,
            int                        zNum,
            CoursePosFlags             extraFlags,
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

                    // X-direction edge: odd rows are reversed so one-way roads alternate
                    if (!isLastCol) {
                        var reverse = row % 2 != 0;
                        var idxA    = row * xNum + col;
                        var idxB    = row * xNum + col + 1;
                        var flagsA  = GridNodeFlags(isFirstRow, isLastRow, isFirstCol, false);
                        var flagsB  = GridNodeFlags(isFirstRow, isLastRow, false,      col + 1 == xNum - 1);
                        var bezier  = NT_EdgeUtils.GenerateStraightEdge(
                            nodes[reverse ? idxB : idxA],
                            nodes[reverse ? idxA : idxB]);
                        curves.Add(new EdgeConfig {
                            Bezier         = bezier,
                            Length         = MathUtils.Length(bezier),
                            StartNodeFlags = (reverse ? flagsB : flagsA) | extraFlags,
                            EndNodeFlags   = (reverse ? flagsA : flagsB) | extraFlags
                        });
                    }

                    // Z-direction edge: odd columns are reversed so one-way roads alternate
                    if (!isLastRow) {
                        var reverse = col % 2 == 0;
                        var idxA    = row * xNum + col;
                        var idxB    = (row + 1) * xNum + col;
                        var flagsA  = GridNodeFlags(isFirstRow, false,               isFirstCol, isLastCol);
                        var flagsB  = GridNodeFlags(false,      row + 1 == zNum - 1, isFirstCol, isLastCol);
                        var bezier  = NT_EdgeUtils.GenerateStraightEdge(
                            nodes[reverse ? idxB : idxA],
                            nodes[reverse ? idxA : idxB]);
                        curves.Add(new EdgeConfig {
                            Bezier         = bezier,
                            Length         = MathUtils.Length(bezier),
                            StartNodeFlags = (reverse ? flagsB : flagsA) | extraFlags,
                            EndNodeFlags   = (reverse ? flagsA : flagsB) | extraFlags
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
                return CoursePosFlags.IsRight;
            }

            return CoursePosFlags.IsParallel;
        }
    }
}
