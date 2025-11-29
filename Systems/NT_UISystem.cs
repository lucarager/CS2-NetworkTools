// <copyright file="NT_UISystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using Colossal.UI.Binding;
    using Extensions;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI;
    using Unity.Collections;
    using Unity.Entities;
    using Utils;

    #endregion

    /// <summary>
    /// System responsible for UI Bindings & Lookup Handling.
    /// </summary>
    public partial class NT_UISystem : ExtendedUISystemBase {
        /// <summary>
        /// Enum to represent the type of selected entity.
        /// </summary>
        public enum SelectedEntityType {
            Unknown = 0,
            Node    = 1,
            Edge    = 2,
        }

        private EntityQuery                              m_ToolPrefabQuery;
        private NameSystem                               m_NameSystem;
        private NT_NodeSelectionToolSystem               m_NodeSelectionToolSystem;
        private PrefabSystem                             m_PrefabSystem;
        private PrefixedLogger                           m_Log;
        private ToolSystem                               m_ToolSystem;
        private ValueBindingHelper<ToolSelectionData[]> m_SelectedEntitiesBinding;
        private ValueBindingHelper<string>               m_SelectedPrefabBinding;
        private ValueBindingHelper<ToolUILookup[]>       m_ToolLookupBinding;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(NT_UISystem));
            m_Log.Debug("OnCreate()");

            m_PrefabSystem            = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ToolSystem              = World.GetOrCreateSystemManaged<ToolSystem>();
            m_NodeSelectionToolSystem = World.GetOrCreateSystemManaged<NT_NodeSelectionToolSystem>();
            m_NameSystem              = World.GetOrCreateSystemManaged<NameSystem>();

            m_ToolLookupBinding       = CreateBinding("UI_DATA", new ToolUILookup[] { });
            m_SelectedPrefabBinding   = CreateBinding("SELECTED_PREFAB", "");
            m_SelectedEntitiesBinding = CreateBinding("SELECTED_ENTITIES", new ToolSelectionData[] { });

            CreateTrigger<string>("SELECT_TOOL", HandleSelectTool);

            m_ToolPrefabQuery = SystemAPI.QueryBuilder()
                                         .WithAll<NT_ToolData>()
                                         .Build();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            var entities        = m_ToolPrefabQuery.ToEntityArray(Allocator.Temp);
            var toolLookupArray = new ToolUILookup[entities.Length];
            for (var i = 0; i < entities.Length; i++) {
                var prefab = m_PrefabSystem.GetPrefab<NT_ToolPrefab>(entities[i]);
                toolLookupArray[i] = new ToolUILookup(prefab);
            }

            m_ToolLookupBinding.Value     = toolLookupArray;
            m_SelectedPrefabBinding.Value = m_ToolSystem.activePrefab != null ? m_ToolSystem.activePrefab.GetPrefabID().GetName() : "";

            // Update selected entities binding
            var selectedNodes        = m_NodeSelectionToolSystem.GetSelectedNodes();
            var selectedEntitiesData = new ToolSelectionData[selectedNodes.Length];

            for (var i = 0; i < selectedNodes.Length; i++) {
                var entity     = selectedNodes[i];
                var entityType = DetermineEntityType(entity);
                var entityName = entityType == SelectedEntityType.Node ? $"Node {i + 1}" : m_NameSystem.GetRenderedLabelName(entity);
                selectedEntitiesData[i] = new ToolSelectionData(entity, entityType, entityName);
            }

            m_SelectedEntitiesBinding.Value = selectedEntitiesData;
        }

        private SelectedEntityType DetermineEntityType(Entity entity) {
            if (EntityManager.HasComponent<Edge>(entity)) {
                return SelectedEntityType.Edge;
            }

            if (EntityManager.HasComponent<Node>(entity)) {
                return SelectedEntityType.Node;
            }

            return SelectedEntityType.Unknown;
        }

        private void HandleSelectTool(string id) {
            m_Log.Debug($"HandleSelectTool(id: {id})");

            if (m_PrefabSystem.TryGetPrefab(
                    new PrefabID(
                        "NT_ToolPrefab",
                        id),
                    out var prefab)) {
                m_ToolSystem.ActivatePrefabTool(prefab);
            }
        }

        /// <summary>
        /// Struct to store and send Zone Lookup and to the React UI.
        /// </summary>
        public readonly struct ToolUILookup : IJsonWritable {
            private readonly NT_ToolPrefab m_Prefab;

            public ToolUILookup(NT_ToolPrefab prefab) { m_Prefab = prefab; }

            /// <inheritdoc/>
            public void Write(IJsonWriter writer) {
                writer.TypeBegin(GetType().FullName);

                writer.PropertyName("DisplayName");
                writer.Write(m_Prefab.DisplayName);

                writer.PropertyName("Icon");
                writer.Write(m_Prefab.Icon);

                writer.PropertyName("ID");
                writer.Write(m_Prefab.GetPrefabID().GetName());

                writer.TypeEnd();
            }
        }

        /// <summary>
        /// Struct to store and send selected entity data to the React UI.
        /// </summary>
        public readonly struct ToolSelectionData : IJsonWritable {
            private readonly Entity             m_Entity;
            private readonly SelectedEntityType m_EntityType;
            private readonly string             m_EntityName;

            public ToolSelectionData(Entity entity, SelectedEntityType entityType, string entityName) {
                m_Entity     = entity;
                m_EntityType = entityType;
                m_EntityName = entityName;
            }

            /// <inheritdoc/>
            public void Write(IJsonWriter writer) {
                writer.TypeBegin(GetType().FullName);

                writer.PropertyName("Entity");
                writer.Write(m_Entity);

                writer.PropertyName("Type");
                writer.Write((int)m_EntityType);

                writer.PropertyName("Name");
                writer.Write(m_EntityName);

                writer.TypeEnd();
            }
        }
    }
}