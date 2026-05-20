namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Collections;
    using Game.Input;
    using Game.Prefabs;
    using Game.Tools;
    using NetworkTools.Components.Tools;
    using Unity.Collections;
    using Unity.Entities;
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

            // Search systems
            m_NetSearchSystem    = World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();
            m_ObjectSearchSystem = World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>();
            m_ZoneSearchSystem   = World.GetOrCreateSystemManaged<Game.Zones.SearchSystem>();
            m_WaterSystem        = World.GetOrCreateSystemManaged<Game.Simulation.WaterSystem>();

            // Data
            m_SelectedControlPoint = new NativeValue<ControlPoint>(Allocator.Persistent);
            m_HoveredControlPoint  = new NativeValue<ControlPoint>(Allocator.Persistent);
            m_SnappedControlPoint  = new NativeValue<ControlPoint>(Allocator.Persistent);
            m_SnappedEntity        = new NativeValue<Entity>(Allocator.Persistent);
            m_SnapLines            = new NativeList<SnapLine>(16, Allocator.Persistent);
        }

        /// <inheritdoc />
        protected override void OnDestroy() {
            if (m_SelectedControlPoint.IsCreated) m_SelectedControlPoint.Dispose();
            if (m_HoveredControlPoint.IsCreated)  m_HoveredControlPoint.Dispose();
            if (m_SnappedControlPoint.IsCreated)  m_SnappedControlPoint.Dispose();
            if (m_SnappedEntity.IsCreated)        m_SnappedEntity.Dispose();
            if (m_SnapLines.IsCreated)            m_SnapLines.Dispose();

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
