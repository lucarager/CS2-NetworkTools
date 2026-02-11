// <copyright file="NT_CEToolSystem.Lifecycle.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license
// information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using NetworkTools.Settings;
    using Unity.Collections;
    using Unity.Jobs;
    using NetworkTools.Settings;

    #endregion

    public partial class NT_SlopeToolSystem {
        public override bool TrySetPrefab(PrefabBase prefab) {
            m_Log.Debug($"TrySetPrefab {prefab is NT_ToolPrefab} {m_PrefabSystem.HasComponent<NetworkTools.Components.NT_Slope>(prefab)}");
            var validRequest =
                prefab is NT_ToolPrefab &&
                m_PrefabSystem.HasComponent<NetworkTools.Components.NT_Slope>(prefab);

            if (!validRequest) return false;

            m_Prefab = prefab;
            return true;
        }

        public override PrefabBase GetPrefab() { return m_Prefab; }

        protected override void OnCreate() {
            // Systems & Tools
            m_Barrier       = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_OverlayRenderSystem =
                World.GetOrCreateSystemManaged<OverlayRenderSystem>();

            // Configuration
            ShowNodes                = true;
            ShowEdges                = true;
            ShowTooltipsSlopes       = true;
            DisableVanillaValidation = true;

            // Actions
            m_ApplyAction = NetworkToolsMod.Instance.Settings.GetAction(NT_Settings.ApplyActionStr);
            m_SecondaryApplyAction =
                NetworkToolsMod.Instance.Settings.GetAction(NT_Settings.SecondaryApplyActionStr);

            // Data Structures
            m_SelectedNodes =
                new NativeList<Unity.Entities.Entity>(32,
                                                                        Allocator
                                                                             .Persistent);
            m_EligibleNodes =
                new NativeList<Unity.Entities.Entity>(64,
                                                                        Allocator
                                                                             .Persistent);
            m_CurrentPathNodes =
                new NativeList<Unity.Entities.Entity>(32,
                                                                        Allocator
                                                                             .Persistent);
            m_CurrentPathEdges =
                new NativeList<Unity.Entities.Entity>(32,
                                                                        Allocator
                                                                             .Persistent);
            m_NextPathNodes =
                new NativeList<Unity.Entities.Entity>(32,
                                                                        Allocator
                                                                             .Persistent);
            m_NextPathEdges =
                new NativeList<Unity.Entities.Entity>(32,
                                                                        Allocator
                                                                             .Persistent);
            m_LastHoveredEntity =
                new NativeReference<Unity.Entities.Entity>(Unity.Collections
                                                                                  .Allocator
                                                                                  .Persistent);
            m_LastRaycastEntity =
                new NativeReference<Unity.Entities.Entity>(Unity.Collections
                                                                                  .Allocator
                                                                                  .Persistent);

            // Queries
            m_DefinitionQuery = GetDefinitionQuery();
            m_NodesWithoutEligibleQuery = Unity.Entities.SystemAPI.QueryBuilder()
                                               .WithAll<Node>()
                                               .WithNone<NetworkTools.Components.NT_Eligible>()
                                               .Build();
            m_NodesWithEligibleQuery = Unity.Entities.SystemAPI.QueryBuilder()
                                            .WithAll<Node, NetworkTools.Components.NT_Eligible>()
                                            .Build();
            m_NodesWithSelectedQuery = Unity.Entities.SystemAPI.QueryBuilder()
                                            .WithAll<Node, NetworkTools.Components.NT_Selected>()
                                            .Build();
            m_NodesWithHighlightedQuery = Unity.Entities.SystemAPI.QueryBuilder()
                                               .WithAll<Node,
                                                   NetworkTools.Components.NT_Highlighted>()
                                               .Build();
            m_NodesWithSelectedFirstQuery = Unity.Entities.SystemAPI.QueryBuilder()
                                                 .WithAll<Node,
                                                     NetworkTools.Components.NT_SelectedFirst>()
                                                 .Build();
            m_NodesWithSelectedLastQuery = Unity.Entities.SystemAPI.QueryBuilder()
                                                .WithAll<Node,
                                                    NetworkTools.Components.NT_SelectedLast>()
                                                .Build();
            m_EdgesWithHighlightedQuery = Unity.Entities.SystemAPI.QueryBuilder()
                                               .WithAll<Edge,
                                                   NetworkTools.Components.NT_Highlighted>()
                                               .Build();
            m_EdgesWithSelectedQuery = Unity.Entities.SystemAPI.QueryBuilder()
                                            .WithAll<Edge, NetworkTools.Components.NT_Selected>()
                                            .Build();

            base.OnCreate();
        }

        protected override void OnDestroy() {
            m_SelectedNodes.Dispose();
            m_EligibleNodes.Dispose();
            m_CurrentPathNodes.Dispose();
            m_CurrentPathEdges.Dispose();
            m_NextPathNodes.Dispose();
            m_NextPathEdges.Dispose();
            m_LastHoveredEntity.Dispose();
            m_LastRaycastEntity.Dispose();

            base.OnDestroy();
        }

        protected override void OnStartRunning() {
            // Reset internal state
            m_LastHitPosition = default;
            m_OperationState  = OperationState.Idle();

            StateTransitionNoNodes();

            m_ApplyAction.shouldBeEnabled          = true;
            m_SecondaryApplyAction.shouldBeEnabled = true;

            base.OnStartRunning();
        }

        protected override void OnStopRunning() {
            // Disable actions
            m_ApplyAction.shouldBeEnabled          = false;
            m_SecondaryApplyAction.shouldBeEnabled = false;

            // Clean up all state components
            m_Log.Debug("OnStopRunning: Cleaning up state components");

            // Batch remove all marker components using cached queries
            EntityManager.RemoveComponent<NetworkTools.Components.NT_Selected>(m_NodesWithSelectedQuery);
            EntityManager.RemoveComponent<NetworkTools.Components.NT_Selected>(m_EdgesWithSelectedQuery);
            EntityManager.RemoveComponent<NetworkTools.Components.NT_Eligible>(m_NodesWithEligibleQuery);
            EntityManager.RemoveComponent<NetworkTools.Components.NT_Highlighted>(m_NodesWithHighlightedQuery);
            EntityManager.RemoveComponent<NetworkTools.Components.NT_Highlighted>(m_EdgesWithHighlightedQuery);
            EntityManager
                .RemoveComponent<NetworkTools.Components.NT_SelectedFirst>(m_NodesWithSelectedFirstQuery);
            EntityManager
                .RemoveComponent<NetworkTools.Components.NT_SelectedLast>(m_NodesWithSelectedLastQuery);

            // Clear internal state
            m_SelectedNodes.Clear();
            m_EligibleNodes.Clear();
            m_CurrentPathNodes.Clear();
            m_CurrentPathEdges.Clear();

            base.OnStopRunning();
        }
    }
}