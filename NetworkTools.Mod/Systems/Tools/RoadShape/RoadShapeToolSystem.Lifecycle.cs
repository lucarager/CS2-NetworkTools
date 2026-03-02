namespace NetworkTools.Systems.Tools.RoadShape {
    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using NetworkTools.Components;
    using NetworkTools.Settings;
    using Unity.Collections;
    using Unity.Entities;
    using Game.Prefabs;
    using NetworkTools.Components;

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
            //RenderEligibleEdges      = true;
            RenderHandles            = true;
            DisableVanillaValidation = true;

            // Data Structures
            m_SelectedNodes    = new NativeList<Entity>(32, Allocator.Persistent);
            m_EligibleNodes    = new NativeList<Entity>(64, Allocator.Persistent);
            m_CurrentPathNodes = new NativeList<Entity>(32, Allocator.Persistent);
            m_CurrentPathEdges = new NativeList<Entity>(32, Allocator.Persistent);
            m_NextPathNodes    = new NativeList<Entity>(32, Allocator.Persistent);
            m_NextPathEdges    = new NativeList<Entity>(32, Allocator.Persistent);

            // Cached path data for handles and jobs
            m_EdgeStates = new NativeList<EdgeState>(32, Allocator.Persistent);
            m_PathDataValid    = false;

            // Override default query to exclude some networks
            m_NodesWithoutEligibleQuery = SystemAPI.QueryBuilder()
                .WithAll<Node, Road>()
                .WithNone<NT_Eligible>()
                .Build();
        }

        protected override void OnDestroy() {
            m_SelectedNodes.Dispose();
            m_EligibleNodes.Dispose();
            m_CurrentPathNodes.Dispose();
            m_CurrentPathEdges.Dispose();
            m_NextPathNodes.Dispose();
            m_NextPathEdges.Dispose();

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
            Phase             = OperationPhase.Idle;

            StateTransitionNoNodes();
        }

        protected override void OnStopRunning() {
            base.OnStopRunning();

            // Tool-specific cleanup
            EntityManager.RemoveComponent<NT_Selected>(m_NodesWithSelectedQuery);
            EntityManager.RemoveComponent<NT_Selected>(m_EdgesWithSelectedQuery);
            EntityManager.RemoveComponent<NT_SelectedFirst>(m_NodesWithSelectedFirstQuery);
            EntityManager.RemoveComponent<NT_SelectedLast>(m_NodesWithSelectedLastQuery);

            // Clear internal state
            m_SelectedNodes.Clear();
            m_EligibleNodes.Clear();
            m_CurrentPathNodes.Clear();
            m_CurrentPathEdges.Clear();

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
