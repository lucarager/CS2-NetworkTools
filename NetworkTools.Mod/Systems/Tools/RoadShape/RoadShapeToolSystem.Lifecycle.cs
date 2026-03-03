namespace NetworkTools.Systems.Tools.RoadShape {
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using NetworkTools.Components;
    using NetworkTools.Components.Tools;

    using Unity.Collections;
    using Unity.Entities;

    public partial class NT_RoadShapeToolSystem {
        public override bool TrySetPrefab(PrefabBase prefab) {
            var hasShapeSlope = m_PrefabSystem.HasComponent<NT_ShapeSlope>(prefab);
            var hasShapeCurve = m_PrefabSystem.HasComponent<NT_ShapeCurve>(prefab);
            m_Log.Debug(
                $"TrySetPrefab {prefab is NT_ToolPrefab} hasShapeSlope={hasShapeSlope} hasShapeCurve={hasShapeCurve}");
            var validRequest =
                prefab is NT_ToolPrefab &&
                (hasShapeSlope || hasShapeCurve);

            if (!validRequest)
            {
                return false;
            }

            m_Prefab = prefab;
            return true;
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_Log.Prefix = nameof(NT_RoadShapeToolSystem);

            // Configuration
            RenderEligibleNodes      = true;
            RenderHandles            = true;
            DisableVanillaValidation = true;

            // Initialize selection state (base class NativeLists)
            InitializeSelectionState();

            // Cached path data for handles and jobs
            m_EdgeStates = new NativeList<EdgeState>(32, Allocator.Persistent);
            m_PathDataValid = false;

            // Override default query to exclude some networks
            m_NodesWithoutEligibleQuery = SystemAPI.QueryBuilder()
                .WithAll<Node, Road>()
                .WithNone<NT_Eligible>()
                .Build();
        }

        protected override void OnDestroy() {
            // Dispose selection state (base class NativeLists)
            DisposeSelectionState();

            // Dispose cached path data
            if (m_EdgeStates.IsCreated) {
                m_EdgeStates.Dispose();
            }

            base.OnDestroy();
        }

        protected override void OnStartRunning() {
            base.OnStartRunning();

            // Reset internal state
            m_LastHitPosition = default;
            Phase = OperationPhase.Idle;

            // Initialize selection state (makes all nodes eligible)
            ResetToNoSelection();
        }

        protected override void OnStopRunning() {
            base.OnStopRunning();

            // Clear selection state
            ClearSelectionState();

            // Invalidate cached path data
            InvalidatePathData();
        }

        public void MarkDirty() {
            m_UpdateNeeded = true;
        }

        /// <summary>
        ///     Sets a new transformation.
        /// </summary>
        public void SetTransformationConfig(ShapeTransformConfig config) {
            ShapeTransformConfig = config;
            m_UpdateNeeded       = true;

            // Enable/Disable rendering based on config
            RenderSlopeTooltips = config.RenderSlopeTooltips;

            // RE-INITIALIZE: Config changed while in Ready phase
            if (Phase == OperationPhase.Ready)
            {
                // InitializeConfig the transform (computes any needed initial values into config)
                InitializeCurrentTransform();

                // Re-create handles using the initialized config
                RefreshTransformHandles();
            }

            m_Log.Debug(
                $"Transformation config set: ShapeTemplate={config.Template}");
        }

        /// <summary>
        ///     Configures the transformation from the UI.
        /// </summary>
        public void UpdateTransformationConfig(ShapeTransformConfig config) {
            ShapeTransformConfig = config;
            m_UpdateNeeded       = true;

            m_Log.Debug(
                $"Transformation config updated: ShapeTemplate={config.Template}");
        }
    }
}
