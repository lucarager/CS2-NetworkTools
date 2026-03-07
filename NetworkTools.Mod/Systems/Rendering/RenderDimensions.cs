namespace NetworkTools.Systems.Rendering {
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    ///     Enum defining all available render color keys for overlay rendering.
    /// </summary>
    public enum RenderDimensionsKey : byte {

    }

    /// <summary>
    ///     Struct containing all render colors for overlay rendering.
    ///     Passed to jobs to provide centralized color configuration.
    /// </summary>
    public readonly struct RenderDimensions {

        /// <summary>
        ///     Gets a color by its key.
        /// </summary>
        public float4 this[RenderDimensionsKey key] => key switch {

        };
    }
}