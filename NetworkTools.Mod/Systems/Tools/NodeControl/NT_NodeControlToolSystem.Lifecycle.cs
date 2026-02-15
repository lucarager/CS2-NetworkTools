// <copyright file="NT_NodeControlToolSystem.Lifecycle.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
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

        protected override void OnCreate() {
            base.OnCreate();

            m_Log.Prefix = nameof(NT_NodeControlToolSystem);

            // Systems
            m_NTPrefabsCreateSystem = World.GetOrCreateSystemManaged<NT_PrefabsCreateSystem>();

            // Configuration
            ShowNodes = true;

            // Data Structures
            m_SelectedNode = new NativeReference<Entity>(Allocator.Persistent);
            m_Markers      = new NativeList<Entity>(16, Allocator.Persistent);

            // Queries
            m_NodesWithSelectedQuery = SystemAPI.QueryBuilder()
                                                .WithAll<Node, NT_Selected>()
                                                .Build();
        }

        protected override void OnDestroy() {
            DestroyMarkers();

            m_SelectedNode.Dispose();
            m_Markers.Dispose();

            base.OnDestroy();
        }

        protected override void OnStartRunning() {
            base.OnStartRunning();

            // Reset internal state
            m_SelectedNode.Value = Entity.Null;

            // Transition to NoSelection state - all nodes become eligible
            StateTransitionNoSelection();
        }
        protected override void OnStopRunning() {
            base.OnStopRunning();

            // Tool-specific cleanup
            EntityManager.RemoveComponent<NT_Selected>(m_NodesWithSelectedQuery);
            DestroyMarkers();

            // Clear internal state
            m_SelectedNode.Value = Entity.Null;
        }
    }
}
