namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Collections;
    using Game.Input;
    using Game.Prefabs;
    using Game.Tools;
    using NetworkTools.Components.Tools;
    using Unity.Collections;
    using Unity.Mathematics;

    /// <summary>
    ///     Lifecycle methods for <see cref="NT_GenerateToolSystem" />.
    /// </summary>
    public partial class NT_GenerateToolSystem {
        /// <inheritdoc />
        public bool HasToolComponent(PrefabBase prefab) {
            return m_PrefabSystem.HasComponent<NT_GenerateTool>(prefab);
        }

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            m_Log.Prefix = nameof(NT_GenerateToolSystem);

            // Configuration
            RenderHandles            = true;
            //DisableVanillaValidation = true;

            // Mode change additionally reinitializes handles
            Mode.OnChanged += _ => {
                if (Phase == OperationPhase.Ready)
                    RebuildHandlesForActiveMode();
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
            Rotation.Value    = new float3(0, 0, 1);

            ElevationBoundParameter = Elevation;

            base.requireNetArrows = true;

            ResetToIdle();
        }

        /// <inheritdoc />
        protected override void OnStopRunning() {
            ElevationBoundParameter = null;

            base.requireNetArrows = false;

            base.OnStopRunning();


            ClearSelectionState();
        }
    }
}
