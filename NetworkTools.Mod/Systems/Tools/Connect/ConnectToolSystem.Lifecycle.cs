namespace NetworkTools.Systems.Tools.Connect {
    using Game.Net;
    using Game.Prefabs;

    using NetworkTools.Components;
    using NetworkTools.Components.Handles;
    using NetworkTools.Components.Tools;
    using NetworkTools.Systems.Tools.Base;
    using NetworkTools.Systems.Tools.RoadShape;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Tool system for
    /// </summary>
    public partial class NT_ConnectToolSystem {
        public override bool TrySetPrefab(PrefabBase prefab) {
            m_Log.Debug($"TrySetPrefab {prefab is NT_ToolPrefab} {m_PrefabSystem.HasComponent<NT_ConnectTool>(prefab)}");

            // If we're setting a NetPrefab, we cache it as the desired prefab for later.
            if (prefab is NetPrefab netPrefab) {
                m_SelectedNetPrefab       = netPrefab;
                m_SelectedNetPrefabEntity = m_PrefabSystem.GetEntity(netPrefab);

                // If the currently active tool is this tool, we want to override the base game NetTool
                if (m_ToolSystem.activeTool is NT_ConnectToolSystem) {
                    return true;
                }

                // Otherwise, we just cache the selected NetPrefab and wait for the user to select the ConnectTool to apply it.
                return false;
            }

            // For non-NetPrefab prefabs, we only accept if it's a NT_ToolPrefab with an NT_Connect component, which indicates it's a valid configuration for this tool.
            var validRequest = prefab is NT_ToolPrefab &&
                               m_PrefabSystem.HasComponent<NT_ConnectTool>(prefab);

            if (!validRequest) {
                return false;
            }

            m_Prefab = prefab;
            return true;
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