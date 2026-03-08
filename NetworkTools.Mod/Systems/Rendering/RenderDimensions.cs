namespace NetworkTools.Systems.Rendering {
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    ///     Render dimensions for overlay rendering. Passed to jobs as a value type.
    /// </summary>
    public struct RenderDimensions {
        /// <summary>
        ///     Standard values.
        /// </summary>
        public static readonly RenderDimensions Default = new RenderDimensions {
        };
    }
}