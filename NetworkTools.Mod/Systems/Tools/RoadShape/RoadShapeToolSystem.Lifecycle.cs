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
            m_Log.Debug(
                $"TrySetPrefab {prefab is NT_ToolPrefab} {m_PrefabSystem.HasComponent<NT_PathTransform>(prefab)}");
            var validRequest =
                prefab is NT_ToolPrefab &&
                m_PrefabSystem.HasComponent<NT_PathTransform>(prefab);

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
            EntityManager.RemoveComponent<NT_Highlighted>(m_EdgesWithHighlightedQuery);
            EntityManager.RemoveComponent<NT_SelectedFirst>(m_NodesWithSelectedFirstQuery);
            EntityManager.RemoveComponent<NT_SelectedLast>(m_NodesWithSelectedLastQuery);

            // Clear internal state
            m_SelectedNodes.Clear();
            m_EligibleNodes.Clear();
            m_CurrentPathNodes.Clear();
            m_CurrentPathEdges.Clear();
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
                CreateTransformHandles(); // Re-creates handles + re-initializes transforms
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
