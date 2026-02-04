// <copyright file="NT_UISystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using Colossal.UI.Binding;
    using Extensions;
    using Game.Input;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI;
    using NetworkTools.Settings;
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

        private EntityQuery                             m_ToolPrefabQuery;
        private NameSystem                              m_NameSystem;
        private NT_SlopeToolSystem                      m_NTSlopeToolSystem;
        private PrefabSystem                            m_PrefabSystem;
        private PrefixedLogger                          m_Log;
        private ToolSystem                              m_ToolSystem;
        private ValueBindingHelper<ToolSelectionData[]> m_SelectedEntitiesBinding;
        private ValueBindingHelper<string>              m_SelectedPrefabBinding;
        private ValueBindingHelper<ToolUILookup[]>      m_ToolLookupBinding;
        private ValueBindingHelper<SlopeConfigData>     m_SlopeConfigBinding;
        private ProxyAction                             m_ToggleToolPanelAction;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(NT_UISystem));
            m_Log.Debug("OnCreate()");

            m_PrefabSystem      = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ToolSystem        = World.GetOrCreateSystemManaged<ToolSystem>();
            m_NTSlopeToolSystem = World.GetOrCreateSystemManaged<NT_SlopeToolSystem>();
            m_NameSystem        = World.GetOrCreateSystemManaged<NameSystem>();

            m_ToolLookupBinding       = CreateBinding("UI_DATA", new ToolUILookup[] { });
            m_SelectedPrefabBinding   = CreateBinding("SELECTED_PREFAB", "");
            m_SelectedEntitiesBinding = CreateBinding("SELECTED_ENTITIES", new ToolSelectionData[] { });
            // todo reset this on tool change
            m_SlopeConfigBinding      = CreateBinding("SLOPE_CONFIG", SlopeConfigData.Default(), HandleUpdateSlopeConfig, new ValueWriter<SlopeConfigData>(), new ValueReader<SlopeConfigData>());

            CreateTrigger<string>("SELECT_TOOL", HandleSelectTool);
            CreateTrigger<string>("APPLY_SLOPE", HandleApplySlope);

            // Actions
            m_ToggleToolPanelAction = NetworkToolsMod.Instance.Settings.GetAction(NT_Settings.ToggleToolPanelStr);

            m_ToolPrefabQuery = SystemAPI.QueryBuilder()
                                         .WithAll<NT_ToolData>()
                                         .Build();

            // Always enable
            m_ToggleToolPanelAction.shouldBeEnabled = true;
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
            var selectedNodes        = m_NTSlopeToolSystem.GetSelectedNodes();
            var selectedEntitiesData = new ToolSelectionData[selectedNodes.Length];

            for (var i = 0; i < selectedNodes.Length; i++) {
                var entity     = selectedNodes[i];
                var entityType = DetermineEntityType(entity);
                var entityName = entityType == SelectedEntityType.Node ? $"Node {i + 1}" : m_NameSystem.GetRenderedLabelName(entity);
                selectedEntitiesData[i] = new ToolSelectionData(entity, entityType, entityName);
            }

            m_SelectedEntitiesBinding.Value = selectedEntitiesData;

            if (m_ToggleToolPanelAction.WasPerformedThisFrame()) {
                // todo
            }

            base.OnUpdate();
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

        private void HandleUpdateSlopeConfig(SlopeConfigData configData) {
            m_Log.Debug($"HandleUpdateSlopeConfig(template: {configData.Template})");
            m_SlopeConfigBinding.Value = configData;
            var config = configData.Template?.ToLowerInvariant() switch {
                "linear" => SlopeCurveConfig.Linear(),
                "easeinout" => SlopeCurveConfig.EaseInOut(configData.EaseInLength, configData.EaseOutLength),
                "parabolic" => SlopeCurveConfig.Parabolic(configData.ArchHeight, configData.ArchPosition),
                _ => SlopeCurveConfig.Linear()
            };
            m_NTSlopeToolSystem.SetTransformationConfig(config);
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

        private void HandleApplySlope(string templateName) { 
            m_Log.Debug($"HandleApplySlope(templateName: {templateName})");
            
            m_NTSlopeToolSystem.RequestApply();
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

                writer.PropertyName("Description");
                writer.Write(m_Prefab.Description);

                writer.PropertyName("Active");
                writer.Write(m_Prefab.Active);

                writer.PropertyName("Index");
                writer.Write(m_Prefab.Index);

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

        /// <summary>
        /// Struct to store and synchronize slope configuration parameters with the UI.
        /// </summary>
        public struct SlopeConfigData : IJsonWritable, IJsonReadable {
            public string Template;
            public float EaseInLength;
            public float EaseOutLength;
            public float ArchHeight;
            public float ArchPosition;

            /// <summary>
            /// Creates default configuration with Linear template.
            /// </summary>
            public static SlopeConfigData Default() => new() {
                Template = "linear",
                EaseInLength = 0.25f,
                EaseOutLength = 0.25f,
                ArchHeight = 0.5f,
                ArchPosition = 0.5f
            };

            /// <inheritdoc/>
            public void Write(IJsonWriter writer) {
                writer.TypeBegin(GetType().FullName);

                writer.PropertyName("template");
                writer.Write(Template);

                writer.PropertyName("easeInLength");
                writer.Write(EaseInLength);

                writer.PropertyName("easeOutLength");
                writer.Write(EaseOutLength);

                writer.PropertyName("archHeight");
                writer.Write(ArchHeight);

                writer.PropertyName("archPosition");
                writer.Write(ArchPosition);

                writer.TypeEnd();
            }

            /// <inheritdoc/>
            public void Read(IJsonReader reader) {
                reader.ReadMapBegin();

                if (reader.ReadProperty("template")) {
                    reader.Read(out string template);
                    Template = template;
                }

                if (reader.ReadProperty("easeInLength")) {
                    reader.Read(out float easeInLength);
                    EaseInLength = easeInLength;
                }

                if (reader.ReadProperty("easeOutLength")) {
                    reader.Read(out float easeOutLength);
                    EaseOutLength = easeOutLength;
                }

                if (reader.ReadProperty("archHeight")) {
                    reader.Read(out float archHeight);
                    ArchHeight = archHeight;
                }

                if (reader.ReadProperty("archPosition")) {
                    reader.Read(out float archPosition);
                    ArchPosition = archPosition;
                }

                reader.ReadMapEnd();
            }
        }
    }
}