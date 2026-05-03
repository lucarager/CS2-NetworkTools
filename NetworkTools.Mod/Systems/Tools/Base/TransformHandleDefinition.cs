namespace NetworkTools.Systems.Tools.Base {
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Parameters;

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
        public float Value;
        public float MinValue;
        public float MaxValue;
        public NT_HandleConstraints? Constraints;

        /// <summary>
        /// Hit detection and visual radius. Defaults to <see cref="NT_Handle.PrimaryRadius"/>.
        /// </summary>
        public float Radius;

        /// <summary>
        /// Normal vector for circle/radius/rotation handles defining the plane orientation.
        /// Defaults to Y-up (0, 1, 0) when zero.
        /// </summary>
        public float3 Normal;

        /// <summary>
        /// Reference direction for rotation handles (zero-angle direction on the plane).
        /// Defaults to +X (1, 0, 0) when zero.
        /// </summary>
        public float3 ReferenceDirection;

        /// <summary>
        /// Initial angle in radians for rotation handles.
        /// </summary>
        public float Angle;

        /// <summary>
        /// Key of the parent handle. When the parent is dragged, this handle moves with it.
        /// Set to <see cref="NoParent"/> (default) for root handles.
        /// </summary>
        public int ParentKey;

        /// <summary>
        /// Direct reference to the parameter this handle controls.
        /// When set, the base system auto-dispatches drag values to the parameter,
        /// eliminating the need for key-based dispatch switches.
        /// </summary>
        public ParameterBase Parameter;
    }
}
