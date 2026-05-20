namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Mathematics;

    using Game.Tools;

    using NetworkTools.Systems.Tools.Utils;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    public struct GridGenerator : IGenerator {
        public void InitializeConfig(ref GenerateJobConfig config) { }

        public void Generate(
            in  GenerateJobConfig      config,
            ref NativeList<EdgeConfig> curves) {
            var netWidth   = config.NetWidth;
            var yOffset    = config.Elevation + config.BaselineElevation;
            var freeHeight = yOffset > -config.ElevationLimit && yOffset < config.ElevationLimit
                ? CoursePosFlags.FreeHeight
                : (CoursePosFlags)0;

            var origin = config.Position + new float3(0, yOffset, 0);
            var xDir   = math.mul(config.StartDirection, new float3(1, 0, 0));
            var zDir   = math.mul(config.StartDirection, new float3(0, 0, 1));
            var xNum   = config.GridXNum;
            var zNum   = config.GridZNum;

            var altXActive = config.AltPrefabX && config.AltNetPrefabXEntity != Entity.Null;
            var altZActive = config.AltPrefabZ && config.AltNetPrefabZEntity != Entity.Null;

            // Node positions use cumulative offsets rather than uniform multiplication
            // because adjacent rows/columns can have different road widths.
            //
            // X-direction roads (horizontal) have their width in the Z dimension, so they
            // determine Z spacing between rows. Z-direction roads (vertical) have their
            // width in the X dimension, so they determine X spacing between columns.
            //
            // Centerline distance between row k and row k+1:
            //   GridZSpacing + width(row k) / 2 + width(row k+1) / 2
            //
            // Centerline distance between column j and column j+1:
            //   GridXSpacing + width(col j) / 2 + width(col j+1) / 2

            var xOffsets = new NativeArray<float>(xNum, Allocator.Temp);
            xOffsets[0] = 0f;
            for (var col = 1; col < xNum; col++)
                xOffsets[col] = xOffsets[col - 1] + config.GridXSpacing
                    + (ColZWidth(col - 1, altZActive, netWidth, config) + ColZWidth(col, altZActive, netWidth, config)) * 0.5f;

            var zOffsets = new NativeArray<float>(zNum, Allocator.Temp);
            zOffsets[0] = 0f;
            for (var row = 1; row < zNum; row++)
                zOffsets[row] = zOffsets[row - 1] + config.GridZSpacing
                    + (RowXWidth(row - 1, altXActive, netWidth, config) + RowXWidth(row, altXActive, netWidth, config)) * 0.5f;

            // Build the flat node grid.
            var nodes = new NativeArray<float3>(xNum * zNum, Allocator.Temp);
            for (var row = 0; row < zNum; row++)
                for (var col = 0; col < xNum; col++)
                    nodes[row * xNum + col] = origin + xDir * xOffsets[col] + zDir * zOffsets[row];

            xOffsets.Dispose();
            zOffsets.Dispose();

            // ── X-direction edges (horizontal, one row at a time) ──────────────────
            // Alternating prefabs are assigned per-row. Each prefab type maintains its
            // own rank so that direction alternation is independent between them.
            // Rank 0 → forward, rank 1 → reversed, rank 2 → forward, …
            var primaryXRank = 0;
            var altXRank     = 0;
            for (var row = 0; row < zNum; row++) {
                var isAlt   = altXActive && (row + 1) % config.AltEveryX == 0;
                var rank    = isAlt ? altXRank : primaryXRank;
                var reverse = rank % 2 != 0;
                var prefab  = isAlt ? config.AltNetPrefabXEntity     : config.NetPrefabEntity;
                var lane    = isAlt ? config.AltNetLanePrefabXEntity : config.NetLanePrefabEntity;

                var isFirstRow = row == 0;
                var isLastRow  = row == zNum - 1;

                for (var col = 0; col < xNum - 1; col++) {
                    var isFirstCol = col == 0;
                    var isLastCol  = col == xNum - 2; // end node sits at xNum-1
                    var idxA       = row * xNum + col;
                    var idxB       = row * xNum + col + 1;
                    var flagsA     = NodeFlags(isFirstRow, isLastRow, isFirstCol, false);
                    var flagsB     = NodeFlags(isFirstRow, isLastRow, false, isLastCol);
                    var bezier     = NT_EdgeUtils.GenerateStraightEdge(
                        nodes[reverse ? idxB : idxA],
                        nodes[reverse ? idxA : idxB]);
                    curves.Add(new EdgeConfig {
                        Bezier              = bezier,
                        Length              = MathUtils.Length(bezier),
                        StartNodeElevation  = yOffset,
                        EndNodeElevation    = yOffset,
                        NetPrefabEntity     = prefab,
                        NetLanePrefabEntity = lane,
                        StartNodeFlags      = (reverse ? flagsB : flagsA) | freeHeight,
                        EndNodeFlags        = (reverse ? flagsA : flagsB) | freeHeight,
                    });
                }

                if (isAlt) altXRank++;
                else       primaryXRank++;
            }

            // ── Z-direction edges (vertical, one column at a time) ─────────────────
            // Same rank-per-prefab approach. Rank 0 → reversed to match the original
            // convention where even columns (rank 0, 2, 4…) were reversed.
            var primaryZRank = 0;
            var altZRank     = 0;
            for (var col = 0; col < xNum; col++) {
                var isAlt   = altZActive && (col + 1) % config.AltEveryZ == 0;
                var rank    = isAlt ? altZRank : primaryZRank;
                var reverse = rank % 2 == 0;
                var prefab  = isAlt ? config.AltNetPrefabZEntity     : config.NetPrefabEntity;
                var lane    = isAlt ? config.AltNetLanePrefabZEntity : config.NetLanePrefabEntity;

                var isFirstCol = col == 0;
                var isLastCol  = col == xNum - 1;

                for (var row = 0; row < zNum - 1; row++) {
                    var isFirstRow = row == 0;
                    var isLastRow  = row == zNum - 2; // end node sits at zNum-1
                    var idxA       = row * xNum + col;
                    var idxB       = (row + 1) * xNum + col;
                    var flagsA     = NodeFlags(isFirstRow, false, isFirstCol, isLastCol);
                    var flagsB     = NodeFlags(false, isLastRow, isFirstCol, isLastCol);
                    var bezier     = NT_EdgeUtils.GenerateStraightEdge(
                        nodes[reverse ? idxB : idxA],
                        nodes[reverse ? idxA : idxB]);
                    curves.Add(new EdgeConfig {
                        Bezier              = bezier,
                        Length              = MathUtils.Length(bezier),
                        StartNodeElevation  = yOffset,
                        EndNodeElevation    = yOffset,
                        NetPrefabEntity     = prefab,
                        NetLanePrefabEntity = lane,
                        StartNodeFlags      = (reverse ? flagsB : flagsA) | freeHeight,
                        EndNodeFlags        = (reverse ? flagsA : flagsB) | freeHeight,
                    });
                }

                if (isAlt) altZRank++;
                else       primaryZRank++;
            }

            nodes.Dispose();
        }

        // Width of the X-direction (horizontal) road at the given row.
        // These roads run in X and have their physical width in the Z dimension,
        // so they determine Z spacing between adjacent node rows.
        private static float RowXWidth(int row, bool altXActive, float primaryWidth, in GenerateJobConfig config) =>
            altXActive && (row + 1) % config.AltEveryX == 0 ? config.AltNetPrefabXWidth : primaryWidth;

        // Width of the Z-direction (vertical) road at the given column.
        // These roads run in Z and have their physical width in the X dimension,
        // so they determine X spacing between adjacent node columns.
        private static float ColZWidth(int col, bool altZActive, float primaryWidth, in GenerateJobConfig config) =>
            altZActive && (col + 1) % config.AltEveryZ == 0 ? config.AltNetPrefabZWidth : primaryWidth;

        private static CoursePosFlags NodeFlags(bool isFirstRow, bool isLastRow, bool isFirstCol, bool isLastCol) {
            if ((isFirstRow || isLastRow) && (isFirstCol || isLastCol))
                return CoursePosFlags.IsFirst | CoursePosFlags.IsLast;
            if (isFirstRow || isLastRow || isFirstCol || isLastCol)
                return CoursePosFlags.IsRight;
            return CoursePosFlags.IsParallel;
        }
    }
}
