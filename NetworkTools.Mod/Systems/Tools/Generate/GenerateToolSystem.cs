namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Collections;
    using Game.Tools;
    using NetworkTools.Systems.Parameters;
    using NetworkTools.Systems.Tools;
    using Unity.Collections;
    using Unity.Mathematics;

    /// <summary>
    ///     Tool system for generating networks.
    /// </summary>
    public partial class NT_GenerateToolSystem : NT_BaseToolSystem, IToolPrefabProvider, INetPrefabCachingProvider, IManualApplyProvider, INetPrefabSelectionProvider {
        /// <inheritdoc />
        public override string toolID => "GenerateTool";

        /// <inheritdoc />
        public override TargetOption AvailableTargets => TargetOption.None;

        // ── Parameters (declarative, reflection-driven) ──────────────────────────

        public EnumParameter<GenerateMode> Mode        = new("generate.mode", GenerateMode.Grid);
        public FloatParameter              GridXSpacing = new("generate.gridXSpacing", 80f, 4f, 500f);
        public FloatParameter              GridZSpacing = new("generate.gridZSpacing", 80f, 4f, 500f);
        public IntParameter                GridXNum     = new("generate.gridXNum", 2, 1, 20);
        public IntParameter                GridZNum     = new("generate.gridZNum", 2, 1, 20);

        // ── Contextual state (set from control point, not UI-driven) ─────────────

        /// <summary>
        ///     Caches the last hit position for tool-specific use.
        /// </summary>
        private float3 m_LastHitPosition;

        /// <summary>
        ///     Hovered control point
        /// </summary>
        protected NativeValue<ControlPoint> m_HoveredControlPoint;

        /// <summary>
        ///     Selected control point
        /// </summary>
        protected NativeValue<ControlPoint> m_SelectedControlPoint;

        /// <summary>
        ///     Start position derived from the placed control point.
        /// </summary>
        internal float3 m_StartPosition;

        /// <summary>
        ///     Start direction derived from the placed control point.
        /// </summary>
        internal quaternion m_StartDirection;
    }
}
