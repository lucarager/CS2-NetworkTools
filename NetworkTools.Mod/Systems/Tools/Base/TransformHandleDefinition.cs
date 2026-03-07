namespace NetworkTools.Systems.Tools.Base {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using NetworkTools.Components.Handles;

    using Unity.Mathematics;

    /// <summary>
    /// Definition for a transform handle.
    /// </summary>
    public struct TransformHandleDefinition {
        /// <summary>
        /// Indicates no parent handle. Matches the struct default for <see cref="ParentKey"/>.
        /// </summary>
        public const int NoParent = 0;

        public int Key;
        public float3 Position;
        public HandleTypeFlags TypeFlags;
        public float Value;        // For parameter handles
        public float MinValue;
        public float MaxValue;
        public NT_HandleConstraints? Constraints;

        /// <summary>
        /// Hit detection and visual radius. Defaults to <see cref="NT_Handle.PrimaryRadius"/>.
        /// </summary>
        public float Radius;

        /// <summary>
        /// Key of the parent handle. When the parent is dragged, this handle moves with it.
        /// Set to <see cref="NoParent"/> (default) for root handles.
        /// </summary>
        public int ParentKey;
    }
}
