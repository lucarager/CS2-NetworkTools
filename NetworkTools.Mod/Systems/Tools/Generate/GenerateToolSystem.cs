namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Collections;
    using Game.Tools;
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

        /// <summary>
        ///     Caches the last hit position for tool-specific use.
        /// </summary>
        private float3 m_LastHitPosition;

        /// <summary>
        ///     Control points
        /// </summary>
        protected NativeList<ControlPoint> m_ControlPoints;

        /// <summary>
        ///     Hovered control point
        /// </summary>
        protected NativeValue<ControlPoint> m_HoveredControlPoint;

        /// <summary>
        ///     Selected control point
        /// </summary>
        protected NativeValue<ControlPoint> m_SelectedControlPoint;

        /// <summary>
        ///     Current configuration.
        /// </summary>
        internal GenerateConfig CurrentConfig = new GenerateConfig();

        /// <summary>
        ///     Currently selected GenerateMode.
        /// </summary>
        public GenerateMode CurrentMode = GenerateMode.Grid;
    }
}
