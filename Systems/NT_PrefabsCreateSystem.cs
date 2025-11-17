// <copyright file="P_PrefabsCreateSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    using System.Collections.Generic;
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Prefabs;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;
    using Utils;

    public partial class NT_PrefabsCreateSystem : GameSystemBase {
        /// <summary>
        /// Stateful value to only run installation once.
        /// </summary>
        private static bool m_PrefabsAreInstalled;

        /// <summary>
        /// Configuration for vanilla prefabas to load for further processing.
        /// </summary>
        private readonly Dictionary<string, PrefabID> m_SourcePrefabsDict = new() {
            { "uiAssetCategory", new PrefabID("UIAssetCategoryPrefab", "ZonesOffice") },
        };

        /// <summary>
        /// Cache for prefabs.
        /// </summary>
        private List<PrefabBase> m_PrefabBases;
        private Dictionary<PrefabBase, Entity> m_PrefabEntities;

        // Systems & References
        private static PrefabSystem m_PrefabSystem;

        // Logger
        private PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();
            m_Log = new PrefixedLogger(nameof(NT_PrefabsCreateSystem));
            m_Log.Debug($"OnCreate()");
            m_PrefabBases = new List<PrefabBase>();
            m_PrefabEntities = new Dictionary<PrefabBase, Entity>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
        }

        /// <inheritdoc/>
        protected override void OnGameLoadingComplete(Purpose  purpose,
                                                      GameMode mode) {
            base.OnGameLoadingComplete(purpose, mode);
            m_Log.Debug($"OnGameLoadingComplete(purpose={purpose}, mode={mode})");

            if (!m_PrefabsAreInstalled) {
                Install();
            }
        }

        /// <inheritdoc/>
        protected override void OnGamePreload(Purpose purpose, GameMode mode) {
            base.OnGamePreload(purpose, mode);
            var logMethodPrefix = $"OnGamePreload(purpose {purpose}, mode {mode}) --";
            m_Log.Debug($"{logMethodPrefix}");

            if (!m_PrefabsAreInstalled) {
                Install();
            }
        }

        private void Install() {
            var logMethodPrefix = $"Install() --";
            
            // Mark the Install as already _prefabsAreInstalled
            m_PrefabsAreInstalled = true;

            var prefabBaseDict = new Dictionary<string, PrefabBase>();

            foreach (var (key, prefabId) in m_SourcePrefabsDict) {
                if (!m_PrefabSystem.TryGetPrefab(prefabId, out var prefabBase)) {
                    m_Log.Error($"{logMethodPrefix} Failed retrieving prefabBase {prefabId} and must exit.");
                    continue;
                }

                prefabBaseDict[key] = prefabBase;
            }

            CreateToolPrefab("Add Node", "coui://nt/add.svg", new NT_AddDelete());
            CreateToolPrefab("Remove Node", "coui://nt/remove.svg", new NT_AddDelete());
            CreateToolPrefab("Create Supernode", "coui://nt/super.svg", new NT_AddDelete());
            CreateToolPrefab("Slope Editor", "coui://nt/slope.svg", new NT_AddDelete());
            CreateToolPrefab("Curve Editor", "coui://nt/curve.svg", new NT_AddDelete());
            CreateToolPrefab("Connect", "coui://nt/connect.svg", new NT_AddDelete());
            CreateToolPrefab("Adv. Parallel", "coui://nt/parallel.svg", new NT_AddDelete());

            m_Log.Debug($"{logMethodPrefix} Completed.");
        }

        private bool CreateToolPrefab<T>(string name, string icon, T component) where T : unmanaged, IComponentLookup {
            var toolPrefabBase = ScriptableObject.CreateInstance<NT_ToolPrefab>();
            toolPrefabBase.name        = name;
            toolPrefabBase.DisplayName = name;
            toolPrefabBase.Description = "DESCRIPTION";
            toolPrefabBase.Icon        = icon;

            var success = m_PrefabSystem.AddPrefab(toolPrefabBase);
        
            if (success) {
                var prefabEntity = m_PrefabSystem.GetEntity(toolPrefabBase);
                EntityManager.AddComponentLookup(prefabEntity, component);
                m_PrefabBases.Add(toolPrefabBase);
                m_PrefabEntities.Add(toolPrefabBase, prefabEntity);
            }

            return success;
        }
    }
}
