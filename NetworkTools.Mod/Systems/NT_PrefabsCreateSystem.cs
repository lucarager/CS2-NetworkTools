// <copyright file="NT_PrefabsCreateSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using System.Collections.Generic;
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Prefabs;
    using Unity.Entities;
    using UnityEngine;
    using Utils;

    #endregion

    public partial class NT_PrefabsCreateSystem : GameSystemBase {
        /// <summary>
        /// Stateful value to only run installation once.
        /// </summary>
        private static bool m_PrefabsAreInstalled;

        // Systems & References
        private static PrefabSystem m_PrefabSystem;

        private int m_InstalledTools = 0;

        /// <summary>
        /// Configuration for vanilla prefabas to load for further processing.
        /// </summary>
        private readonly Dictionary<string, PrefabID> m_SourcePrefabsDict = new() {
            { "marker", new PrefabID("MarkerObjectPrefab", "Pedestrian Access Location") },
        };

        private Dictionary<PrefabBase, Entity> m_PrefabEntities;

        /// <summary>
        /// Cache for prefabs.
        /// </summary>
        private List<PrefabBase> m_PrefabBases;

        internal Entity m_HandlePrefabEntity;

        // Logger
        private PrefixedLogger m_Log;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();
            m_Log = new PrefixedLogger(nameof(NT_PrefabsCreateSystem));
            m_Log.Debug("OnCreate()");
            m_PrefabBases    = new List<PrefabBase>();
            m_PrefabEntities = new Dictionary<PrefabBase, Entity>();
            m_PrefabSystem   = World.GetOrCreateSystemManaged<PrefabSystem>();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
        }

        /// <inheritdoc/>
        protected override void OnGameLoadingComplete(Purpose purpose,
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
            var logMethodPrefix = "Install() --";

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

            CreateToolPrefab("Add Node",         "add.svg",      "", new Components.NT_AddNode(),       true);
            CreateToolPrefab("Remove Node",      "remove.svg",   "", new Components.NT_RemoveNode(),    true);
            CreateToolPrefab("Create Supernode", "super.svg",    "", new Components.NT_Select(),        false);
            CreateToolPrefab("Path Shape Tools", "slope.svg",    "", new Components.NT_PathTransform(), true);
            CreateToolPrefab("Secret Tools",     "curve.svg",    "", new Components.NT_NodeControl(),   true);
            CreateToolPrefab("Connect",          "connect.svg",  "", new Components.NT_Select(),        false);
            CreateToolPrefab("Adv. Parallel",    "parallel.svg", "", new Components.NT_Select(),        false);

            CreateHandlePrefab((MarkerObjectPrefab)prefabBaseDict["marker"]);

            m_Log.Debug($"{logMethodPrefix} Completed.");
        }

        private bool CreateHandlePrefab(MarkerObjectPrefab pedestrianPrefab) {
            var prefabBase = ScriptableObject.CreateInstance<MarkerObjectPrefab>();
            prefabBase.name       = "NT_HandleObjectPrefab";
            prefabBase.m_Circular = true;
            prefabBase.m_Mesh = pedestrianPrefab.m_Mesh;

            var success = m_PrefabSystem.AddPrefab(prefabBase);

            if (success) {
                var prefabEntity = m_PrefabSystem.GetEntity(prefabBase);
                m_PrefabBases.Add(prefabBase);
                m_PrefabEntities.Add(prefabBase, prefabEntity);
                m_HandlePrefabEntity = prefabEntity;
            }

            return success;
        }

        private bool CreateToolPrefab<T>(string name, string icon, string description, T component, bool active)
            where T : unmanaged, IComponentData {
            var toolPrefabBase = ScriptableObject.CreateInstance<NT_ToolPrefab>();
            toolPrefabBase.name        = name;
            toolPrefabBase.DisplayName = name;
            toolPrefabBase.Description = description;
            toolPrefabBase.Icon        = icon;
            toolPrefabBase.Active      = active;
            toolPrefabBase.Index       = m_InstalledTools++;

            var success = m_PrefabSystem.AddPrefab(toolPrefabBase);

            if (success) {
                var prefabEntity = m_PrefabSystem.GetEntity(toolPrefabBase);
                EntityManager.AddComponentData(prefabEntity, component);
                m_PrefabBases.Add(toolPrefabBase);
                m_PrefabEntities.Add(toolPrefabBase, prefabEntity);
            }

            return success;
        }
    }
}