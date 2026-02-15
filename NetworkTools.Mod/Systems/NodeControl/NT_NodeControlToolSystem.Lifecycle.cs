// <copyright file="NT_NodeControlToolSystem.Lifecycle.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using NetworkTools.Components;
    using Unity.Collections;
    using Unity.Entities;

    #endregion

    public partial class NT_NodeControlToolSystem {
        public override bool TrySetPrefab(PrefabBase prefab) {
            m_Log.Debug($"TrySetPrefab {prefab is NT_ToolPrefab} {m_PrefabSystem.HasComponent<NT_NodeControl>(prefab)}");
            var validRequest = prefab is NT_ToolPrefab && m_PrefabSystem.HasComponent<NT_NodeControl>(prefab);

            if (!validRequest) {
                return false;
            }

            m_Prefab = prefab;
            return true;
        }

        public override PrefabBase GetPrefab() => m_Prefab;

        protected override void OnCreate() {
            base.OnCreate();

            m_Log.Prefix = nameof(NT_NodeControlToolSystem);

            // Systems
            m_Barrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_NTPrefabsCreateSystem = World.GetOrCreateSystemManaged<NT_PrefabsCreateSystem>();

            // Configuration
            ShowNodes = true;

            // Actions
            m_ApplyAction          = NetworkToolsMod.Instance.Settings.GetAction(Settings.NT_Settings.ApplyActionStr);
            m_SecondaryApplyAction = NetworkToolsMod.Instance.Settings.GetAction(Settings.NT_Settings.SecondaryApplyActionStr);

            // Data Structures
            m_SelectedNode      = new NativeReference<Entity>(Allocator.Persistent);
            m_LastHoveredEntity = new NativeReference<Entity>(Allocator.Persistent);
            m_Markers           = new NativeList<Entity>(16, Allocator.Persistent);

            // Queries
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
            m_NodesWithSelectedQuery = SystemAPI.QueryBuilder()
                                                .WithAll<Node, NT_Selected>()
                                                .Build();
            m_DefinitionQuery = GetDefinitionQuery();

        }

        protected override void OnDestroy() {
            DestroyMarkers();

            m_SelectedNode.Dispose();
            m_LastHoveredEntity.Dispose();
            m_Markers.Dispose();

            base.OnDestroy();
        }

        protected override void OnStartRunning() {
            // Reset internal state
            m_SelectedNode.Value      = Entity.Null;
            m_LastHoveredEntity.Value = Entity.Null;

            // Transition to NoSelection state - all nodes become eligible
            StateTransitionNoSelection();

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
            EntityManager.RemoveComponent<NT_Selected>(m_NodesWithSelectedQuery);
            EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);
            EntityManager.RemoveComponent<NT_Highlighted>(m_NodesWithHighlightedQuery);

            // Clear internal state
            m_SelectedNode.Value      = Entity.Null;
            m_LastHoveredEntity.Value = Entity.Null;

            DestroyMarkers();

            base.OnStopRunning();
        }
    }
}
