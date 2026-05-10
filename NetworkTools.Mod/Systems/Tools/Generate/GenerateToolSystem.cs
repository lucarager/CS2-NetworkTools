namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Collections;

    using Game.Tools;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.Handles;
    using NetworkTools.Systems.Tools.Parameters;

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

        // Grid
        public EnumParameter<GenerateMode> Mode         = new("generate.mode", GenerateMode.Grid);
        public FloatParameter              GridXSpacing = new("generate.gridXSpacing", 60f, 4f, 240f, modes: (int)GenerateMode.Grid);
        public FloatParameter              GridZSpacing = new("generate.gridZSpacing", 60f, 4f, 240f, modes: (int)GenerateMode.Grid);
        public IntParameter                GridXNum     = new("generate.gridXNum", 2, 1, 20, modes: (int)GenerateMode.Grid);
        public IntParameter GridZNum = new("generate.gridZNum", 2, 1, 20, modes: (int)GenerateMode.Grid);
        // Circle
        public FloatParameter              CircleRadius = new("generate.circleRadius", 60f, 4f, 240f, modes: (int)GenerateMode.Circle);
        // Shared, Contextual (from control point placement + handle drags)
        public Float3Parameter      StartPosition  = new("generate.startPosition", modes: (int)GenerateMode.Grid | (int)GenerateMode.Circle) {
            Handles = new IHandleSpec<float3>[] { new PositionHandle() }
        };

        public QuaternionParameter StartDirection =
            new("generate.startDirection", modes: (int)GenerateMode.Grid | (int)GenerateMode.Circle);

        /// <inheritdoc />
        protected override int GetActiveModeFlag() => (int)Mode.Value;

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
    }
}
