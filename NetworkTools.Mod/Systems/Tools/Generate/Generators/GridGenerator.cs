namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Mathematics;

    using Game.Tools;

    using NetworkTools.Systems.Tools.RoadShape;
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
            var freeHeight = config.FollowTerrain
                ? CoursePosFlags.FreeHeight
                : (CoursePosFlags)0;
            var nodeElevation = config.FollowTerrain ? yOffset : SlopeUtils.ClampElevation(yOffset);

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
                    + (ColZWidth(col - 1, altXActive, netWidth, config) + ColZWidth(col, altXActive, netWidth, config)) * 0.5f;

            var zOffsets = new NativeArray<float>(zNum, Allocator.Temp);
            zOffsets[0] = 0f;
            for (var row = 1; row < zNum; row++)
                zOffsets[row] = zOffsets[row - 1] + config.GridZSpacing
                    + (RowXWidth(row - 1, altZActive, netWidth, config) + RowXWidth(row, altZActive, netWidth, config)) * 0.5f;

            // Build the flat node grid.
            var nodes = new NativeArray<float3>(xNum * zNum, Allocator.Temp);
            for (var row = 0; row < zNum; row++)
                for (var col = 0; col < xNum; col++)
                    nodes[row * xNum + col] = origin + xDir * xOffsets[col] + zDir * zOffsets[row];

            // ── X-direction edges (horizontal, one row at a time) ──────────────────
            // Alternating prefabs are assigned per-row. Each prefab type maintains its
            // own rank so that direction alternation is independent between them.
            // Rank 0 → forward, rank 1 → reversed, rank 2 → forward, …
            var primaryXRank = 0;
            var altZRank     = 0;
            for (var row = 0; row < zNum; row++) {
                var isAlt   = altZActive && (row + 1) % config.AltEveryZ == 0;
                var rank    = isAlt ? altZRank : primaryXRank;
                var reverse = rank % 2 != 0;
                var prefab  = isAlt ? config.AltNetPrefabZEntity     : config.NetPrefabEntity;
                var lane    = isAlt ? config.AltNetLanePrefabZEntity : config.NetLanePrefabEntity;
                var xWidth  = RowXWidth(row, altZActive, netWidth, config);

                var isBoundaryRow = row == 0 || row == zNum - 1;

                for (var col = 0; col < xNum - 1; col++) {
                    var idxA = row * xNum + col;
                    var idxB = row * xNum + col + 1;

                    var flagsA = CoursePosFlags.IsGrid;
                    var flagsB = CoursePosFlags.IsGrid;
                    if (row != 0) {
                        flagsA |= CoursePosFlags.IsParallel;
                        flagsB |= CoursePosFlags.IsParallel;
                    }
                    if (isBoundaryRow) {
                        flagsA |= CoursePosFlags.IsRight;
                        flagsB |= CoursePosFlags.IsRight;
                        if (col == 0)        flagsA |= CoursePosFlags.IsFirst;
                        if (col == xNum - 2) flagsB |= CoursePosFlags.IsLast;
                    }
                    var posA       = nodes[idxA];
                    var posB       = nodes[idxB];
                    var xHalf      = xWidth * 0.5f;
                    var shiftStart = (8f - (xHalf + xOffsets[col]) % 8f) % 8f;
                    var shiftEnd   = (xHalf + xOffsets[col + 1]) % 8f;
                    posA += xDir * shiftStart;
                    posB -= xDir * shiftEnd;

                    var bezier     = NT_EdgeUtils.GenerateStraightEdge(
                        reverse ? posB : posA,
                        reverse ? posA : posB);
                    curves.Add(new EdgeConfig {
                        StartNodePosition   = nodes[reverse ? idxB : idxA],
                        EndNodePosition     = nodes[reverse ? idxA : idxB],
                        Bezier              = bezier,
                        Length              = MathUtils.Length(bezier),
                        StartNodeElevation  = nodeElevation,
                        EndNodeElevation    = nodeElevation,
                        NetPrefabEntity     = prefab,
                        NetLanePrefabEntity = lane,
                        StartNodeFlags      = (reverse ? flagsB : flagsA) | freeHeight,
                        EndNodeFlags        = (reverse ? flagsA : flagsB) | freeHeight,
                    });
                }

                if (isAlt) altZRank++;
                else       primaryXRank++;
            }

            // ── Z-direction edges (vertical, one column at a time) ─────────────────
            // Same rank-per-prefab approach. Rank 0 → reversed to match the original
            // convention where even columns (rank 0, 2, 4…) were reversed.
            var primaryZRank = 0;
            var altXRank     = 0;
            for (var col = 0; col < xNum; col++) {
                var isAlt   = altXActive && (col + 1) % config.AltEveryX == 0;
                var rank    = isAlt ? altXRank : primaryZRank;
                var reverse = rank % 2 == 0;
                var prefab  = isAlt ? config.AltNetPrefabXEntity     : config.NetPrefabEntity;
                var lane    = isAlt ? config.AltNetLanePrefabXEntity : config.NetLanePrefabEntity;
                var zWidth  = ColZWidth(col, altXActive, netWidth, config);

                var isBoundaryCol = col == 0 || col == xNum - 1;

                for (var row = 0; row < zNum - 1; row++) {
                    var idxA = row * xNum + col;
                    var idxB = (row + 1) * xNum + col;

                    var flagsA = CoursePosFlags.IsGrid;
                    var flagsB = CoursePosFlags.IsGrid;
                    if (col != xNum - 1) {
                        flagsA |= CoursePosFlags.IsParallel;
                        flagsB |= CoursePosFlags.IsParallel;
                    }
                    if (isBoundaryCol) {
                        flagsA |= CoursePosFlags.IsRight;
                        flagsB |= CoursePosFlags.IsRight;
                        if (row == 0)        flagsA |= CoursePosFlags.IsFirst;
                        if (row == zNum - 2) flagsB |= CoursePosFlags.IsLast;
                    }
                    var posA       = nodes[idxA];
                    var posB       = nodes[idxB];
                    var zHalf      = zWidth * 0.5f;
                    var shiftStart = (8f - (zHalf + zOffsets[row]) % 8f) % 8f;
                    var shiftEnd   = (zHalf + zOffsets[row + 1]) % 8f;
                    posA += zDir * shiftStart;
                    posB -= zDir * shiftEnd;

                    var bezier     = NT_EdgeUtils.GenerateStraightEdge(
                        reverse ? posB : posA,
                        reverse ? posA : posB);
                    curves.Add(new EdgeConfig {
                        StartNodePosition   = nodes[reverse ? idxB : idxA],
                        EndNodePosition     = nodes[reverse ? idxA : idxB],
                        Bezier              = bezier,
                        Length              = MathUtils.Length(bezier),
                        StartNodeElevation  = nodeElevation,
                        EndNodeElevation    = nodeElevation,
                        NetPrefabEntity     = prefab,
                        NetLanePrefabEntity = lane,
                        StartNodeFlags      = (reverse ? flagsB : flagsA) | freeHeight,
                        EndNodeFlags        = (reverse ? flagsA : flagsB) | freeHeight,
                    });
                }

                if (isAlt) altXRank++;
                else       primaryZRank++;
            }

            nodes.Dispose();
            xOffsets.Dispose();
            zOffsets.Dispose();
        }

        // Width of the X-direction (horizontal) road at the given row.
        // AltPrefabZ alternates rows, so we check AltEveryZ here.
        private static float RowXWidth(int row, bool altZActive, float primaryWidth, in GenerateJobConfig config) =>
            altZActive && (row + 1) % config.AltEveryZ == 0 ? config.AltNetPrefabZWidth : primaryWidth;

        // Width of the Z-direction (vertical) road at the given column.
        // AltPrefabX alternates columns, so we check AltEveryX here.
        private static float ColZWidth(int col, bool altXActive, float primaryWidth, in GenerateJobConfig config) =>
            altXActive && (col + 1) % config.AltEveryX == 0 ? config.AltNetPrefabXWidth : primaryWidth;

    }
}
