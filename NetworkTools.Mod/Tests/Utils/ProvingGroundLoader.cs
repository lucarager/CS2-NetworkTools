namespace NetworkTools.Tests {
    using Game.Simulation;

    using Unity.Entities;
    using Unity.Mathematics;

    using UnityEngine;
    using UnityEngine.Experimental.Rendering;

    /// <summary>
    ///     Puts the <see cref="ProvingGround" /> under the loaded map, as the editor imports one.
    ///     A test needs no map of its own: any empty map becomes the terrain it expects.
    /// </summary>
    internal static class ProvingGroundLoader {
        /// <summary>
        ///     Replaces the heights of the loaded map.
        /// </summary>
        /// <param name="world">The world of the loaded map.</param>
        public static void Apply(World world) {
            var terrain = world.GetOrCreateSystemManaged<TerrainSystem>();
            var heights = new ushort[ProvingGround.Resolution * ProvingGround.Resolution];

            ProvingGround.Fill(heights);

            var map = new Texture2D(
                ProvingGround.Resolution,
                ProvingGround.Resolution,
                GraphicsFormat.R16_UNorm,
                TextureCreationFlags.None);

            map.SetPixelData(heights, 0);
            map.Apply(false);

            // The height scale first: the game reads the heightmap against it.
            terrain.SetTerrainProperties(new float2(ProvingGround.HeightScale, 0f));
            terrain.ReplaceHeightmap(map);

            // The game keeps a copy.
            Object.Destroy(map);
        }
    }
}
