namespace NetworkTools.Systems.Tools {
    using Game.Prefabs;

    using NetworkTools.Systems.Tools;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Tool system for generating networks.
    /// </summary>
    public partial class NT_GenerateToolSystem : NT_BaseToolSystem, IToolPrefabProvider, INetPrefabCachingProvider, IManualApplyProvider, INetPrefabSelectionProvider {
        /// <inheritdoc />
        public override string toolID => "GenerateTool";

        /// <inheritdoc />
        public override TargetOption AvailableTargets => TargetOption.Road | TargetOption.Path;

        /// <summary>
        ///     Caches the last hit position for tool-specific use.
        /// </summary>
        private float3 m_LastHitPosition;

        /// <summary>
        ///     Control points defining the grid origin and direction.
        ///     Index 0 = start, Index 1 = end.
        /// </summary>
        protected NativeList<float3> m_ControlPoints;

        /// <summary>
        ///     Current grid configuration.
        /// </summary>
        internal GenerateConfig CurrentConfig = new GenerateConfig();

        /// <summary>
        ///     Gets the current selection state based on the number of control points.
        /// </summary>
        public SelectionState CurrentSelectionState =>
            m_ControlPoints.Length switch {
                0 => SelectionState.NoSelection,
                1 => SelectionState.StartNodeSelected,
                _ => SelectionState.EndNodeSelected
            };
    }
}
