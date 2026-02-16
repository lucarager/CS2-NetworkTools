// <copyright file="NT_CEToolSystem.Lifecycle.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license
// information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using NetworkTools.Components;
    using NetworkTools.Settings;
    using Unity.Collections;
    using Unity.Entities;

    #endregion

    public partial class NT_PathTransformToolSystem {
        public override bool TrySetPrefab(PrefabBase prefab) {
            m_Log.Debug(
                $"TrySetPrefab {prefab is NT_ToolPrefab} {m_PrefabSystem.HasComponent<NT_PathTransform>(prefab)}");
            var validRequest =
                prefab is NT_ToolPrefab &&
                m_PrefabSystem.HasComponent<NT_PathTransform>(prefab);

            if (!validRequest) {
                return false;
            }

            m_Prefab = prefab;
            return true;
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_Log.Prefix = nameof(NT_PathTransformToolSystem);

            // Configuration
            ShowNodes                = true;
            ShowEdges                = true;
            ShowTooltipsSlopes       = true;
            DisableVanillaValidation = true;

            // Data Structures
            m_SelectedNodes = new NativeList<Entity>(32, Allocator.Persistent);
            m_EligibleNodes = new NativeList<Entity>(64, Allocator.Persistent);
            m_CurrentPathNodes = new NativeList<Entity>(32, Allocator.Persistent);
            m_CurrentPathEdges = new NativeList<Entity>(32, Allocator.Persistent);
            m_NextPathNodes = new NativeList<Entity>(32, Allocator.Persistent);
            m_NextPathEdges = new NativeList<Entity>(32, Allocator.Persistent);

            // Queries
            m_NodesWithSelectedQuery = SystemAPI.QueryBuilder()
                .WithAll<Node, NT_Selected>()
                .Build();
            m_NodesWithSelectedFirstQuery = SystemAPI.QueryBuilder()
                .WithAll<Node, NT_SelectedFirst>()
                .Build();
            m_NodesWithSelectedLastQuery = SystemAPI.QueryBuilder()
                .WithAll<Node, NT_SelectedLast>()
                .Build();
            m_EdgesWithHighlightedQuery = SystemAPI.QueryBuilder()
                .WithAll<Edge, NT_Highlighted>()
                .Build();
            m_EdgesWithSelectedQuery = SystemAPI.QueryBuilder()
                .WithAll<Edge, NT_Selected>()
                .Build();

            // Override default query to exclude some networks
            m_NodesWithoutEligibleQuery = SystemAPI.QueryBuilder()
                .WithAll<Node, ConnectedEdge>()
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
    }
}