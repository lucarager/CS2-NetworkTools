namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Collections;
    using Game.Input;
    using Game.Prefabs;
    using Game.Tools;
    using NetworkTools.Components.Tools;
    using NetworkTools.Systems.Tools.Parameters;
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
            RenderHandles = true;

            // Mode change resets to Idle so the player re-clicks for the new mode's semantics
            Mode.OnChanged += _ => {
                if (Phase != OperationPhase.Idle)
                    ResetToIdle();
            };

            // Handle → ControlPoint sync: when Position is dragged, update CP[0]
            Position.OnChanged += origin => {
                if (origin != ChangeOrigin.Handle || m_ControlPoints.Length < 1) return;
                UpdateControlPointPosition(0, Position.Value);
            };

            // Handle → ControlPoint sync: when GridDirectionPoint is dragged, update CP[1] and re-derive
            GridDirectionPoint.OnChanged += origin => {
                if (origin != ChangeOrigin.Handle || m_ControlPoints.Length < 2) return;
                UpdateControlPointPosition(1, GridDirectionPoint.Value);
                InitializeFromSecondPoint(GridDirectionPoint.Value);
            };

            // Handle → ControlPoint sync: when OvalAxisPoint is dragged, update CP[1] and re-derive
            OvalAxisPoint.OnChanged += origin => {
                if (origin != ChangeOrigin.Handle || m_ControlPoints.Length < 2) return;
                UpdateControlPointPosition(1, OvalAxisPoint.Value);
                InitializeFromSecondPoint(OvalAxisPoint.Value);
            };

            // Handle → ControlPoint sync: when CircleRadius is dragged via CircleHandle, re-derive CP[1]
            CircleRadius.OnChanged += origin => {
                if (origin != ChangeOrigin.Handle || m_ControlPoints.Length < 2) return;
                // Update CP[1] to lie at the new radius distance from Position
                var dir = m_ControlPoints[1].m_Position - Position.Value;
                var normalizedDir = math.lengthsq(dir.xz) > 0.001f
                    ? math.normalizesafe(new float3(dir.x, 0, dir.z))
                    : new float3(1, 0, 0);
                UpdateControlPointPosition(1, Position.Value + normalizedDir * CircleRadius.Value);
            };

            // Search systems
            m_NetSearchSystem    = World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();
            m_ObjectSearchSystem = World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>();
            m_ZoneSearchSystem   = World.GetOrCreateSystemManaged<Game.Zones.SearchSystem>();
            m_WaterSystem        = World.GetOrCreateSystemManaged<Game.Simulation.WaterSystem>();

            // Data
            m_ControlPoints       = new NativeList<ControlPoint>(2, Allocator.Persistent);
            m_SnappedControlPoint = new NativeValue<ControlPoint>(Allocator.Persistent);
            m_SnappedEntity       = new NativeValue<Entity>(Allocator.Persistent);
            m_SnapLines           = new NativeList<SnapLine>(16, Allocator.Persistent);
        }

        /// <inheritdoc />
        protected override void OnDestroy() {
            if (m_ControlPoints.IsCreated)       m_ControlPoints.Dispose();
            if (m_SnappedControlPoint.IsCreated) m_SnappedControlPoint.Dispose();
            if (m_SnappedEntity.IsCreated)       m_SnappedEntity.Dispose();
            if (m_SnapLines.IsCreated)           m_SnapLines.Dispose();

            base.OnDestroy();
        }

        /// <inheritdoc />
        protected override void OnStartRunning() {
            base.OnStartRunning();

            Rotation.Value = new float3(0, 0, 1);
            ElevationBoundParameter = Elevation;
            base.requireNetArrows = true;

            ResetToIdle();
        }

        /// <inheritdoc />
        protected override void OnStopRunning() {
            ElevationBoundParameter = null;
            base.requireNetArrows = false;

            base.OnStopRunning();

            ResetToIdle();
        }

        /// <summary>
        ///     Updates a control point's position in the list.
        /// </summary>
        private void UpdateControlPointPosition(int index, float3 position) {
            var cp = m_ControlPoints[index];
            cp.m_Position    = position;
            cp.m_HitPosition = position;
            m_ControlPoints[index] = cp;
        }

        /// <summary>
        ///     Derives the operation phase from the number of placed control points.
        /// </summary>
        private OperationPhase DerivePhase() => m_ControlPoints.Length switch {
            0 => OperationPhase.Idle,
            1 => OperationPhase.Configuring,
            _ => OperationPhase.Ready
        };
    }
}
