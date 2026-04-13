namespace NetworkTools.Systems.Tools {
    using Game.Prefabs;

    using NetworkTools.Components.Tools;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Lifecycle methods for <see cref="NT_GridToolSystem"/>.
    /// </summary>
    public partial class NT_GridToolSystem {
        /// <inheritdoc />
        public bool HasToolComponent(PrefabBase prefab) { return m_PrefabSystem.HasComponent<NT_GridTool>(prefab); }

        /// <summary>
        ///     Initializes the grid config from the two control points.
        /// </summary>
        private void InitializeConfig() {
            m_Log.Debug("InitializeConfig");

            if (Phase != OperationPhase.Ready) {
                return;
            }

            CurrentConfig = new GridConfig(m_ControlPoints[0], m_ControlPoints[1]);
            RefreshTransformHandles();
        }

        /// <summary>
        ///     Updates the grid configuration from the UI without reinitializing handles.
        /// </summary>
        /// <param name="config">The updated config from the UI.</param>
        public void UpdateConfig(GridConfig config) {
            CurrentConfig.Angle    = config.Angle;
            CurrentConfig.XSpacing = config.XSpacing;
            CurrentConfig.ZSpacing = config.ZSpacing;
            CurrentConfig.XNum     = config.XNum;
            CurrentConfig.ZNum     = config.ZNum;
            m_UpdateNeeded         = true;
        }

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            m_Log.Prefix = nameof(NT_GridToolSystem);

            // Configuration
            RenderHandles            = true;
            DisableVanillaValidation = true;

            // Data
            m_ControlPoints = new NativeList<float3>(2, Allocator.Persistent);
        }

        /// <inheritdoc />
        protected override void OnDestroy() {
            if (m_ControlPoints.IsCreated) m_ControlPoints.Dispose();

            base.OnDestroy();
        }

        /// <inheritdoc />
        protected override void OnStartRunning() {
            base.OnStartRunning();

            // Reset internal state
            m_LastHitPosition = default;
            Phase             = OperationPhase.Idle;

            ResetToIdle();
        }

        /// <inheritdoc />
        protected override void OnStopRunning() {
            base.OnStopRunning();

            ClearSelectionState();
        }
    }
}
