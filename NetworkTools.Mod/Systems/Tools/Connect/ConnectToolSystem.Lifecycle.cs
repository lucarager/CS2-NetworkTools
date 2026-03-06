namespace NetworkTools.Systems.Tools.Connect {
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;

    using NetworkTools.Components;
    using NetworkTools.Components.Tools;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.RoadShape;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Tool system for
    /// </summary>
    public partial class NT_ConnectToolSystem {
        public override bool TrySetPrefab(PrefabBase prefab) {
            m_Log.Debug($"TrySetPrefab {m_ToolSystem.activeTool}");
            m_Log.Debug($"TrySetPrefab {m_ToolSystem.activePrefab}");
            m_Log.Debug($"TrySetPrefab {prefab}");
            m_Log.Debug($"TrySetPrefab {prefab is NT_ToolPrefab} {m_PrefabSystem.HasComponent<NT_Connect>(prefab)}");

            // If we're setting a NetPrefab, we cache it as the desired prefab for later.
            if (prefab is NetPrefab netPrefab) {
                m_SelectedNetPrefab = netPrefab;
                m_SelectedNetPrefabEntity = m_PrefabSystem.GetEntity(netPrefab);
                
                // If the currently active tool is this tool, we want to override the base game NetTool
                if (m_ToolSystem.activeTool is NT_ConnectToolSystem) {
                    return true;
                } else {
                    // Otherwise, we just cache the selected NetPrefab and wait for the user to select the ConnectTool to apply it.
                }
                return false;
            }

            // For non-NetPrefab prefabs, we only accept if it's a NT_ToolPrefab with an NT_Connect component, which indicates it's a valid configuration for this tool.
            var validRequest = prefab is NT_ToolPrefab &&
                               m_PrefabSystem.HasComponent<NT_Connect>(prefab);

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
            RenderEligibleNodes = true;
            RenderHandles = true;
            DisableVanillaValidation = true;

            // Data
            m_SelectedNodes = new NativeList<Entity>(32, Allocator.Persistent);

            // Override default query to exclude some networks
            m_NodesWithoutEligibleQuery = SystemAPI.QueryBuilder()
                .WithAll<Node>()
                .WithAny<Road, LocalConnect>()
                .WithNone<NT_Eligible>()
                .Build();
        }

        protected override void OnDestroy() {
            if (m_SelectedNodes.IsCreated) m_SelectedNodes.Dispose();

            base.OnDestroy();
        }

        protected override void OnStartRunning() {
            base.OnStartRunning();

            // Reset internal state
            m_LastHitPosition = default;
            Phase = OperationPhase.Idle;

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
