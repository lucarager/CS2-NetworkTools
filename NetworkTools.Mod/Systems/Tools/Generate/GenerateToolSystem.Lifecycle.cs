namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Collections;
    using Game.Prefabs;
    using Game.Tools;
    using NetworkTools.Components.Tools;
    using Unity.Collections;

    /// <summary>
    ///     Lifecycle methods for <see cref="NT_GenerateToolSystem" />.
    /// </summary>
    public partial class NT_GenerateToolSystem {
        /// <inheritdoc />
        public bool HasToolComponent(PrefabBase prefab) {
            return m_PrefabSystem.HasComponent<NT_GenerateTool>(prefab);
        }

        /// <summary>
        ///     Initializes contextual state from the placed control point
        ///     and resets grid parameters to defaults.
        /// </summary>
        private void InitializeConfig() {
            m_Log.Debug("InitializeConfig");

            if (Phase != OperationPhase.Ready) {
                return;
            }

            var point = m_SelectedControlPoint.value;

            StartPosition.Value  = point.m_Position;
            StartDirection.Value = point.m_Rotation;

            GridXSpacing.ResetToDefault();
            GridZSpacing.ResetToDefault();
            GridXNum.ResetToDefault();
            GridZNum.ResetToDefault();

            RefreshTransformHandles();
        }

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            m_Log.Prefix = nameof(NT_GenerateToolSystem);

            // Configuration
            RenderHandles            = true;
            DisableVanillaValidation = true;

            // Parameter changes trigger a preview rebuild
            foreach (var p in Parameters)
                p.OnChanged += () => m_UpdateNeeded = true;

            // Mode change additionally reinitializes context and handles
            Mode.OnChanged += () => {
                if (Phase == OperationPhase.Ready)
                    InitializeConfig();
            };

            // Data
            m_SelectedControlPoint = new NativeValue<ControlPoint>(Allocator.Persistent);
            m_HoveredControlPoint  = new NativeValue<ControlPoint>(Allocator.Persistent);
        }

        /// <inheritdoc />
        protected override void OnDestroy() {
            if (m_SelectedControlPoint.IsCreated) {
                m_SelectedControlPoint.Dispose();
            }

            if (m_HoveredControlPoint.IsCreated) {
                m_HoveredControlPoint.Dispose();
            }

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
