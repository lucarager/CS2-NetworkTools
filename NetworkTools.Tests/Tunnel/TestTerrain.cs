namespace NetworkTools.Tests.Tunnel {
    using System;
    using System.Runtime.InteropServices;

    using Game.Simulation;

    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;

    /// <summary>
    ///     A rectangle of ground whose height a function gives, as the game hands it to a tool.
    ///     The heights live in managed memory, so the terrain needs no native allocation.
    ///     A cell is one metre wide, and a slope that breaks on a whole metre is exact.
    /// </summary>
    internal sealed unsafe class TestTerrain : IDisposable {
        /// <summary>
        ///     Extent of the ground along x, in metres.
        /// </summary>
        private const int Width = 1024;

        /// <summary>
        ///     Extent of the ground along z, in metres.
        /// </summary>
        public const int Depth = 64;

        /// <summary>
        ///     Height the heightmap's largest value stands for.
        /// </summary>
        private const float MaxHeight = 256f;

        /// <summary>
        ///     Pin on the managed heights, so that the terrain data may point into them.
        /// </summary>
        private GCHandle m_Heights;

        /// <summary>
        ///     The terrain data laid over the heights.
        /// </summary>
        private TerrainHeightData m_Data;

        /// <summary>
        ///     Builds the ground from its height at each whole metre.
        /// </summary>
        /// <param name="height">Height of the ground at a given x and z.</param>
        public TestTerrain(Func<float, float, float> height) {
            var heights = new ushort[Width * Depth];
            var scale   = ushort.MaxValue / MaxHeight;

            for (var z = 0; z < Depth; z++) {
                for (var x = 0; x < Width; x++) {
                    heights[z * Width + x] = (ushort)math.round(height(x, z) * scale);
                }
            }

            Pin(
                heights,
                new int3(Width, ushort.MaxValue + 1, Depth),
                new float3(1f, scale, 1f),
                default);
        }

        private TestTerrain() {
        }

        /// <summary>
        ///     Builds the <see cref="ProvingGround" /> as the game holds it once loaded.
        ///     The game puts a post at the middle of its cell, half a cell from the map's edge.
        /// </summary>
        /// <returns>The terrain, the same heights at the same places as in the game.</returns>
        public static TestTerrain OfProvingGround() {
            var posts   = ProvingGround.Resolution;
            var heights = new ushort[posts * posts];
            var cells   = 1f / ProvingGround.CellSize;
            var edge    = posts * ProvingGround.CellSize * 0.5f - ProvingGround.CellSize * 0.5f;
            var terrain = new TestTerrain();

            ProvingGround.Fill(heights);

            terrain.Pin(
                heights,
                new int3(posts, ushort.MaxValue + 1, posts),
                new float3(cells, ushort.MaxValue / ProvingGround.HeightScale, cells),
                new float3(edge, 0f, edge));

            return terrain;
        }

        /// <summary>
        ///     Pins the heights and lays the terrain's native array over them, with no copy.
        ///     They stay pinned until <see cref="Dispose" />.
        /// </summary>
        /// <param name="heights">One value for each post, row after row.</param>
        /// <param name="resolution">Posts along x, the value range, and posts along z.</param>
        /// <param name="scale">Posts per metre across, and values per metre of height.</param>
        /// <param name="offset">Added to a world position before the scale.</param>
        private void Pin(ushort[] heights, int3 resolution, float3 scale, float3 offset) {
            m_Heights = GCHandle.Alloc(heights, GCHandleType.Pinned);

            var native = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<ushort>(
                (void*)m_Heights.AddrOfPinnedObject(),
                heights.Length,
                Allocator.None);

            m_Data = new TerrainHeightData(native, default, resolution, scale, offset, false);
        }

        /// <summary>
        ///     Gets the terrain heights to pass by reference.
        /// </summary>
        public ref TerrainHeightData Data {
            get {
                return ref m_Data;
            }
        }

        /// <inheritdoc />
        public void Dispose() {
            m_Heights.Free();
        }
    }
}
