// <copyright file="NT_UISystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using Colossal.UI.Binding;
    using Extensions;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Utils;

    #endregion

    /// <summary>
    /// System responsible for UI Bindings & Lookup Handling.
    /// </summary>
    public partial class NT_UISystem : ExtendedUISystemBase {
        private EntityQuery                              m_ToolPrefabQuery;
        private PrefabSystem                             m_PrefabSystem;
        private PrefixedLogger                           m_Log;
        private ToolSystem                               m_ToolSystem;
        private NT_NodeSelectionToolSystem               m_NodeSelectionToolSystem;
        private ValueBindingHelper<string>               m_SelectedPrefabBinding;
        private ValueBindingHelper<ToolUILookup[]>       m_ToolLookupBinding;
        private ValueBindingHelper<SelectedEntityData[]> m_SelectedEntitiesBinding;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(NT_UISystem));
            m_Log.Debug("OnCreate()");

            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ToolSystem   = World.GetOrCreateSystemManaged<ToolSystem>();
            m_NodeSelectionToolSystem = World.GetOrCreateSystemManaged<NT_NodeSelectionToolSystem>();

            m_ToolLookupBinding     = CreateBinding("UI_DATA", new ToolUILookup[] { });
            m_SelectedPrefabBinding = CreateBinding("SELECTED_PREFAB", "");
            m_SelectedEntitiesBinding = CreateBinding("SELECTED_ENTITIES", new SelectedEntityData[] { });
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
            var selectedNodes = m_NodeSelectionToolSystem.GetSelectedNodes();
            var selectedEntitiesData = new SelectedEntityData[selectedNodes.Length];
            for (var i = 0; i < selectedNodes.Length; i++) {
                selectedEntitiesData[i] = new SelectedEntityData(selectedNodes[i]);
            }
            m_SelectedEntitiesBinding.Value = selectedEntitiesData;
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
        public readonly struct SelectedEntityData : IJsonWritable {
            private readonly Entity m_Entity;

            public SelectedEntityData(Entity entity) { m_Entity = entity; }

            /// <inheritdoc/>
            public void Write(IJsonWriter writer) {
                writer.TypeBegin(GetType().FullName);

                writer.PropertyName("Index");
                writer.Write(m_Entity.Index);

                writer.PropertyName("Version");
                writer.Write(m_Entity.Version);

                writer.TypeEnd();
            }
        }
    }
}