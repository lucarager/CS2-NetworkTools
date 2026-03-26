namespace NetworkTools.Systems.Tools.Connect {
    using Game.Net;
    using Game.Prefabs;

    using NetworkTools.Components;
    using NetworkTools.Components.Handles;
    using NetworkTools.Components.Tools;
    using NetworkTools.Systems.Tools.Base;
    using NetworkTools.Systems.Tools.Parallel;
    using NetworkTools.Systems.Tools.RoadShape;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Tool system for
    /// </summary>
    public partial class NT_ConnectToolSystem {
        /// <inheritdoc />
        public bool HasToolComponent(PrefabBase prefab) { return m_PrefabSystem.HasComponent<NT_ConnectTool>(prefab); }

        /// <inheritdoc />
        public bool? TryCacheNetPrefab(PrefabBase prefab) {
            switch (prefab) {
                case RoadPrefab or TrackPrefab or WaterwayPrefab or PathwayPrefab:
                {
                    m_SelectedNetPrefab           = (NetPrefab)prefab;
                    m_SelectedNetPrefabEntity     = m_PrefabSystem.GetEntity(m_SelectedNetPrefab);
                    m_SelectedNetLanePrefab       = null;
                    m_SelectedNetLanePrefabEntity = Entity.Null;
                    return m_ToolSystem.activeTool == this;
                }
                case NetLanePrefab netLanePrefab:
                {
                    m_SelectedNetLanePrefab       = netLanePrefab;
                    m_SelectedNetLanePrefabEntity = m_PrefabSystem.GetEntity(netLanePrefab);
                    m_SelectedNetPrefab           = null;
                    m_SelectedNetPrefabEntity     = Entity.Null;
                    return m_ToolSystem.activeTool == this;
                }
                default:
                    return null;
            }
        }

        /// <summary>
        ///     Sets a new transformation.
        /// </summary>
        public void SetMode(ConnectMode mode) {
            CurrentMode    = mode;
            m_UpdateNeeded = true;

            // Re-initialize configs
            InitializeConfig();

            m_Log.Debug($"Mode set: mode={mode}");
        }


        /// <summary>
        ///     Calls InitializeConfig on the current generator to compute initial values.
        /// </summary>
        private void InitializeConfig() {
            m_Log.Debug($"InitializeConfig: Initializing {CurrentMode}");

            // Only initialize config when we have 2 valid nodes selected (Ready phase)
            if (Phase != OperationPhase.Ready) {
                return;
            }

            var startNodeEntity = m_SelectedNodes[0];
            var endNodeEntity   = m_SelectedNodes[1];
            var startNode       = EntityManager.GetComponentData<Node>(startNodeEntity);
            var endNode         = EntityManager.GetComponentData<Node>(endNodeEntity);
            var startPosition   = startNode.m_Position;
            var endPosition     = endNode.m_Position;

            // Calculate initial direction as the horizontal vector pointing towards the other node.
            // Flatten to XZ so the direction stays level regardless of elevation difference.
            var delta = endPosition - startPosition;
            delta.y = 0f;
            var horizontalDir = math.normalizesafe(delta, new float3(1, 0, 0));

            var startDirection = horizontalDir;
            var endDirection   = -horizontalDir;

            var config = new ConnectConfig(startPosition, endPosition, startDirection, endDirection);

            // Each transform's InitializeConfig method may modify ShapeTransformConfig
            // to store computed values needed for handles and transformation.
            switch (CurrentMode) {
                case ConnectMode.SimpleCurve:
                    new SimpleCurveGenerator().InitializeConfig(in CurrentMode, ref config);
                    break;
                case ConnectMode.Loop:
                    new LoopGenerator().InitializeConfig(in CurrentMode, ref config);
                    break;
            }

            CurrentConfig = config;

            RefreshTransformHandles();
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_Log.Prefix = nameof(NT_ConnectToolSystem);

            // Configuration
            RenderEligibleNodes      = true;
            RenderHandles            = true;
            DisableVanillaValidation = true;

            // Data
            m_SelectedNodes = new NativeList<Entity>(32, Allocator.Persistent);
        }

        protected override void OnDestroy() {
            if (m_SelectedNodes.IsCreated) m_SelectedNodes.Dispose();

            base.OnDestroy();
        }

        protected override void OnStartRunning() {
            base.OnStartRunning();

            // Reset internal state
            m_LastHitPosition = default;
            Phase             = OperationPhase.Idle;

            // Initialize selection state (makes all nodes eligible)
            ResetToIdle();
        }

        protected override void OnStopRunning() {
            base.OnStopRunning();

            // Clear selection state
            ClearSelectionState();
        }
    }
}