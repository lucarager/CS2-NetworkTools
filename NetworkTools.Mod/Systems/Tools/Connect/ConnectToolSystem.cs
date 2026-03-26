namespace NetworkTools.Systems.Tools.Connect {
    using Game.Prefabs;

    using NetworkTools.Systems.Tools;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Tool system for
    /// </summary>
    public partial class NT_ConnectToolSystem : NT_BaseToolSystem, IToolPrefabProvider, INetPrefabCachingProvider, INodeSelectionProvider, INetPrefabSelectionProvider {
        /// <inheritdoc />
        public override string toolID => "ConnectTool";

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
        ///     List of user-selected node entities that define path endpoints.
        /// </summary>
        protected NativeList<Entity> m_SelectedNodes;

        /// <summary>
        ///     Selected net Prefab.
        /// </summary>
        protected NetPrefab m_SelectedNetPrefab;

        /// <summary>
        ///     Selected net Prefab entity.
        /// </summary>
        protected Entity m_SelectedNetPrefabEntity;

        /// <summary>
        ///     Selected net lane prefab for parallel road segments.
        /// </summary>
        protected NetLanePrefab m_SelectedNetLanePrefab;

        /// <summary>
        ///     Selected net lane prefab entity.
        /// </summary>
        protected Entity m_SelectedNetLanePrefabEntity;

        /// <inheritdoc />
        public NetPrefab SelectedNetPrefab => m_SelectedNetPrefab;

        /// <inheritdoc />
        public Entity SelectedNetPrefabEntity => m_SelectedNetPrefabEntity;

        /// <summary>
        ///     Gets the current selection state based on the number of selected nodes.
        /// </summary>
        public SelectionState CurrentSelectionState =>
            m_SelectedNodes.Length switch {
                0 => SelectionState.NoSelection,
                1 => SelectionState.StartNodeSelected,
                _ => SelectionState.EndNodeSelected
            };

        /// <summary>
        ///     Gets a value indicating whether a complete path is selected (2+ nodes).
        /// </summary>
        public bool HasCompletePath => m_SelectedNodes.Length >= 2;

        /// <summary>
        ///     Gets the start node of the selection, or Entity.Null if none selected.
        /// </summary>
        public Entity StartNode => m_SelectedNodes.Length > 0 ? m_SelectedNodes[0] : Entity.Null;

        /// <summary>
        ///     Gets the end node of the selection, or Entity.Null if less than 2 nodes selected.
        /// </summary>
        public Entity EndNode => m_SelectedNodes.Length >= 2 ? m_SelectedNodes[^1] : Entity.Null;

        /// <inheritdoc />
        public Entity[] GetSelectedNodes() {
            return m_SelectedNodes.IsCreated
                       ? m_SelectedNodes.ToArray(Allocator.Temp).ToArray()
                       : System.Array.Empty<Entity>();
        }

        /// <summary>
        ///     Currently selected ConnectMode.
        /// </summary>
        public ConnectMode CurrentMode = ConnectMode.None;

        /// <summary>
        ///     Current config.
        /// </summary>
        internal ConnectConfig CurrentConfig = new ConnectConfig();
    }
}
