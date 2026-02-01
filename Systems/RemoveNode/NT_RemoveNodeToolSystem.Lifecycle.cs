// <copyright file="NT_CEToolSystem.Lifecycle.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using Settings;
    using Unity.Collections;
    using Unity.Entities;

    #endregion

    public partial class NT_RemoveNodeToolSystem {
        public override bool TrySetPrefab(PrefabBase prefab) {
            m_Log.Debug($"TrySetPrefab {prefab is NT_ToolPrefab} {m_PrefabSystem.HasComponent<NT_RemoveNode>(prefab)}");
            var validRequest = prefab is NT_ToolPrefab && m_PrefabSystem.HasComponent<NT_RemoveNode>(prefab);

            if (!validRequest) {
                return false;
            }

            m_Prefab = prefab;
            return true;
        }

        public override PrefabBase GetPrefab() { return m_Prefab; }

        protected override void OnCreate() {
            // Systems & Tools
            m_Barrier             = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_TerrainSystem       = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();

            // Configuration
            ShowNodes = true;

            // Actions
            m_ApplyAction          = NetworkToolsMod.Instance.Settings.GetAction("ApplyActionName");

            // Data Structures
            m_LastHoveredEntity = new NativeReference<Entity>(Allocator.Persistent);
            m_LastRaycastEntity = new NativeReference<Entity>(Allocator.Persistent);

            // Queries
            m_DefinitionQuery = GetDefinitionQuery();
            m_NodesWithoutEligibleQuery = SystemAPI.QueryBuilder()
                                                   .WithAll<Node>()
                                                   .WithNone<NT_Eligible>()
                                                   .Build();
            m_NodesWithEligibleQuery = SystemAPI.QueryBuilder()
                                                .WithAll<Node, NT_Eligible>()
                                                .Build();
            m_NodesWithHighlightedQuery = SystemAPI.QueryBuilder()
                                                   .WithAll<Node, NT_Highlighted>()
                                                   .Build();

            base.OnCreate();
        }

        protected override void OnDestroy() {
            m_LastHoveredEntity.Dispose();
            m_LastRaycastEntity.Dispose();

            base.OnDestroy();
        }

        protected override void OnStartRunning() {
            m_OperationState  = OperationState.Idle();

            m_ApplyAction.shouldBeEnabled = true;
        }

        protected override void OnStopRunning() {
            m_ApplyAction.shouldBeEnabled = false;

            // Clean up all state components
            m_Log.Debug("OnStopRunning: Cleaning up state components");

            // Batch remove all marker components using cached queries
            EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);
            EntityManager.RemoveComponent<NT_Highlighted>(m_NodesWithHighlightedQuery);
        }
    }
}