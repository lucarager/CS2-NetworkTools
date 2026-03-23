namespace NetworkTools.Systems.Tools {
    using Game.Prefabs;

    using NetworkTools.Systems.Tools;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Tool system for generating road grids.
    ///     Two control points define the grid origin and direction; configurable properties
    ///     (Angle, X Spacing, Y Spacing) are exposed via handles and the UI.
    /// </summary>
    public partial class NT_GridToolSystem : NT_BaseToolSystem, IManualApplyProvider, INetPrefabSelectionProvider {
        /// <inheritdoc />
        public override string toolID => "GridTool";

        /// <inheritdoc />
        public override TargetOption AvailableTargets => TargetOption.Road | TargetOption.Path;

        /// <summary>
        ///     Caches the last hit position for tool-specific use.
        /// </summary>
        private float3 m_LastHitPosition;

        /// <summary>
        ///     Tracks whether an update/re-render is needed on the next frame.
        /// </summary>
        private bool m_UpdateNeeded;

        /// <summary>
        ///     Control points defining the grid origin and direction.
        ///     Index 0 = start, Index 1 = end.
        /// </summary>
        protected NativeList<float3> m_ControlPoints;

        /// <summary>
        ///     Selected net prefab for grid road segments.
        /// </summary>
        protected NetPrefab m_SelectedNetPrefab;

        /// <summary>
        ///     Selected net prefab entity.
        /// </summary>
        protected Entity m_SelectedNetPrefabEntity;

        /// <inheritdoc />
        public NetPrefab SelectedNetPrefab => m_SelectedNetPrefab;

        /// <inheritdoc />
        public Entity SelectedNetPrefabEntity => m_SelectedNetPrefabEntity;

        /// <summary>
        ///     Current grid configuration.
        /// </summary>
        internal GridConfig CurrentConfig = new GridConfig();

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
