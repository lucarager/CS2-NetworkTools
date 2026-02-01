// <copyright file="NT_CEToolSystem.Lifecycle.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license
// information.
// </copyright>

using Enumerable = System.Linq.Enumerable;

namespace NetworkTools.Systems;

#region Using Statements

using Game.Net;
using Game.Prefabs;
using Game.Rendering;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Colossal.Serialization.Entities;

#endregion

public partial class NT_CeToolSystem {
    public override bool TrySetPrefab(PrefabBase prefab) {
        m_Log.Debug($"TrySetPrefab {prefab is NT_ToolPrefab} {m_PrefabSystem.HasComponent<NT_Select>(prefab)}");
        var validRequest =
            prefab is NT_ToolPrefab && m_PrefabSystem.HasComponent<NT_Select>(prefab);

        if (!validRequest) return false;

        m_Prefab = prefab;
        return true;
    }

    public override PrefabBase GetPrefab() { return m_Prefab; }

    protected override void OnGameLoadingComplete(Purpose purpose, Game.GameMode mode) {
        base.OnGameLoadingComplete(purpose, mode);

        if (m_RegisteredWithAnarchy) return;

        var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies) {
            if (!assembly.GetName().FullName.Contains("Anarchy,")) continue;


            m_Log.Info($"{nameof(NT_CeToolSystem)}.Lifecycle.{nameof(OnGameLoadingComplete)} Found Anarchy Assembly: {assembly.FullName}.");
            var anarchyBridge = Enumerable.FirstOrDefault(assembly.GetTypes(),
                                                          x =>
                                                              x.FullName != null && x.FullName
                                                                                     .Contains("Anarchy.Bridge.AnarchyBridge"));
            if (anarchyBridge is null) {
                m_Log.Info($"{nameof(NT_CeToolSystem)}.Lifecycle.{nameof(OnGameLoadingComplete)} Couldn't locate Anarchy Bridge.");
                continue;
            }

            m_Log.Debug($"{nameof(NT_CeToolSystem)}.Lifecycle.{nameof(OnGameLoadingComplete)} Located Anarchy Bridge.");

            var addToolMethod = anarchyBridge.GetMethod("TryAddToolSystem",
                                                        System.Reflection.BindingFlags.Public |
                                                        System.Reflection.BindingFlags.Static);
            if (addToolMethod is null) {
                m_Log.Info($"{nameof(NT_CeToolSystem)}.Lifecycle.{nameof(OnGameLoadingComplete)} Could not find method to add tool.");
                break;
            }

            var results = addToolMethod.Invoke(null, new object[] { this });
            if (results is true) {
                m_RegisteredWithAnarchy = true;
                m_Log.Info($"{nameof(NT_CeToolSystem)}.Lifecycle.{nameof(OnGameLoadingComplete)} Successfully registered with Anarchy!");
            } else {
                m_Log.Info($"{nameof(NT_CeToolSystem)}.Lifecycle.{nameof(OnGameLoadingComplete)} Failed to register with Anarchy.");
            }
        }
    }

    protected override void OnCreate() {
        // Systems & Tools
        m_Barrier             = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
        m_TerrainSystem       = World.GetOrCreateSystemManaged<TerrainSystem>();
        m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();

        // Configuration
        ShowNodes          = true;
        ShowEdges          = true;
        ShowTooltipsSlopes = true;

        // Actions
        m_ApplyAction = NetworkToolsMod.Instance.Settings.GetAction("ApplyActionName");
        m_SecondaryApplyAction =
            NetworkToolsMod.Instance.Settings.GetAction("SecondaryApplyActionName");

        // Data Structures
        m_SelectedNodes     = new NativeList<Entity>(32, Allocator.Persistent);
        m_EligibleNodes     = new NativeList<Entity>(64, Allocator.Persistent);
        m_CurrentPathNodes  = new NativeList<Entity>(32, Allocator.Persistent);
        m_CurrentPathEdges  = new NativeList<Entity>(32, Allocator.Persistent);
        m_NextPathNodes     = new NativeList<Entity>(32, Allocator.Persistent);
        m_NextPathEdges     = new NativeList<Entity>(32, Allocator.Persistent);
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
        m_NodesWithSelectedQuery = SystemAPI.QueryBuilder()
                                            .WithAll<Node, NT_Selected>()
                                            .Build();
        m_NodesWithHighlightedQuery = SystemAPI.QueryBuilder()
                                               .WithAll<Node, NT_Highlighted>()
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
        m_LastHitPosition = default;
        m_OperationState  = OperationState.Idle();

        StateTransitionNoNodes();

        m_ApplyAction.shouldBeEnabled          = true;
        m_SecondaryApplyAction.shouldBeEnabled = true;
    }

    protected override void OnStopRunning() {
        m_ApplyAction.shouldBeEnabled          = false;
        m_SecondaryApplyAction.shouldBeEnabled = false;

        // Clean up all state components
        m_Log.Debug("OnStopRunning: Cleaning up state components");

        // Batch remove all marker components using cached queries
        EntityManager.RemoveComponent<NT_Selected>(m_NodesWithSelectedQuery);
        EntityManager.RemoveComponent<NT_Selected>(m_EdgesWithSelectedQuery);
        EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);
        EntityManager.RemoveComponent<NT_Highlighted>(m_NodesWithHighlightedQuery);
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