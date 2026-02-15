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
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.PathTransform;
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
        private NT_PathTransformToolSystem              m_NtPathTransformToolSystem;
        private PrefabSystem                            m_PrefabSystem;
        private PrefixedLogger                          m_Log;
        private ToolSystem                              m_ToolSystem;
        private ValueBindingHelper<ToolSelectionData[]> m_SelectedEntitiesBinding;
        private ValueBindingHelper<string>              m_SelectedPrefabBinding;
        private ValueBindingHelper<ToolUILookup[]>      m_ToolUIDataBinding;
        private ValueBindingHelper<SlopeConfigData>     m_SlopeConfigBinding;
        private ValueBindingHelper<ShapeConfigData>     m_ShapeConfigBinding;
        private ProxyAction                             m_ToggleToolPanelAction;
        private string                                  m_LastSelectedPrefab;
        private int                                     m_LastToolPrefabCount;
        private int                                     m_LastSelectedNodesHash;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(NT_UISystem));
            m_Log.Debug("OnCreate()");

            m_PrefabSystem      = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ToolSystem        = World.GetOrCreateSystemManaged<ToolSystem>();
            m_NtPathTransformToolSystem = World.GetOrCreateSystemManaged<NT_PathTransformToolSystem>();
            m_NameSystem        = World.GetOrCreateSystemManaged<NameSystem>();

            m_ToolUIDataBinding       = CreateBinding("UI_DATA", new ToolUILookup[] { });
            m_SelectedPrefabBinding   = CreateBinding("SELECTED_PREFAB", "");
            m_SelectedEntitiesBinding = CreateBinding("SELECTED_ENTITIES", new ToolSelectionData[] { });
            m_SlopeConfigBinding      = CreateBinding("SLOPE_CONFIG", SlopeConfigData.Default(), HandleUpdateSlopeConfig, new ValueWriter<SlopeConfigData>(), new ValueReader<SlopeConfigData>());
            m_ShapeConfigBinding      = CreateBinding("SHAPE_CONFIG", ShapeConfigData.Default(), HandleUpdateShapeConfig, new ValueWriter<ShapeConfigData>(), new ValueReader<ShapeConfigData>());

            CreateTrigger<string>("SELECT_TOOL", HandleSelectTool);
            CreateTrigger<string>("APPLY_SLOPE", HandleApplySlope);

            // Actions
            //m_ToggleToolPanelAction = NetworkToolsMod.Instance.Settings.GetAction(NT_Settings.ToggleToolPanelStr);

            m_ToolPrefabQuery = SystemAPI.QueryBuilder()
                                         .WithAll<Components.NT_ToolData>()
                                         .Build();

            // Always enable
            //m_ToggleToolPanelAction.shouldBeEnabled = true;
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            // Update tool UI data when the prefab count changes
            var entityCount = m_ToolPrefabQuery.CalculateEntityCount();
            if (entityCount != m_LastToolPrefabCount) {
                m_LastToolPrefabCount = entityCount;
                var entities        = m_ToolPrefabQuery.ToEntityArray(Allocator.Temp);
                var toolLookupArray = new ToolUILookup[entities.Length];
                for (var i = 0; i < entities.Length; i++) {
                    var prefab = m_PrefabSystem.GetPrefab<NT_ToolPrefab>(entities[i]);
                    toolLookupArray[i] = new ToolUILookup(prefab);
                }

                m_ToolUIDataBinding.Value = toolLookupArray;
            }

            // Update selected prefab binding when it changes
            var currentPrefab = m_ToolSystem.activePrefab != null ? m_ToolSystem.activePrefab.GetPrefabID().GetName() : "";
            if (currentPrefab != m_LastSelectedPrefab) {
                m_LastSelectedPrefab          = currentPrefab;
                m_SelectedPrefabBinding.Value = currentPrefab;
            }

            // Update selected entities binding when selection changes
            var selectedNodes    = m_NtPathTransformToolSystem.GetSelectedNodes();
            var currentNodesHash = ComputeSelectionHash(selectedNodes);
            if (currentNodesHash != m_LastSelectedNodesHash) {
                m_LastSelectedNodesHash = currentNodesHash;
                var selectedEntitiesData = new ToolSelectionData[selectedNodes.Length];

                for (var i = 0; i < selectedNodes.Length; i++) {
                    var entity     = selectedNodes[i];
                    var entityType = DetermineEntityType(entity);
                    var entityName = entityType == SelectedEntityType.Node ? $"Node {i + 1}" : m_NameSystem.GetRenderedLabelName(entity);
                    selectedEntitiesData[i] = new ToolSelectionData(entity, entityType, entityName);
                }

                m_SelectedEntitiesBinding.Value = selectedEntitiesData;
            }

            //if (m_ToggleToolPanelAction.WasPerformedThisFrame()) {
            //    // todo
            //}

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

        private int ComputeSelectionHash(Entity[] entities) {
            unchecked {
                var hash = 17;
                for (var i = 0; i < entities.Length; i++) {
                    hash = hash * 31 + entities[i].Index;
                    hash = hash * 31 + entities[i].Version;
                }

                return hash;
            }
        }

        private void HandleUpdateSlopeConfig(SlopeConfigData configData) {
            m_Log.Debug($"HandleUpdateSlopeConfig(template: {configData.Template})");
            m_SlopeConfigBinding.Value = configData;
            var slopeConfig = configData.Template?.ToLowerInvariant() switch {
                "preserve" => SlopeCurveConfig.Preserve(),
                "linear" => SlopeCurveConfig.Linear(),
                "easeinout" => SlopeCurveConfig.EaseInOut(configData.EaseInLength, configData.EaseOutLength),
                "parabolic" => SlopeCurveConfig.Parabolic(configData.ArchHeight, configData.ArchPosition),
                _ => SlopeCurveConfig.Linear(),
            };

            // Build transform config combining shape and slope
            var shapeData = m_ShapeConfigBinding.Value;
            var shapeConfig = shapeData.Template?.ToLowerInvariant() switch {
                "preserve" => ShapeCurveConfig.Preserve(),
                "straighten" => ShapeCurveConfig.Straighten(),
                "smooth" => ShapeCurveConfig.Smooth(shapeData.SmoothingFactor),
                _ => ShapeCurveConfig.Preserve(),
            };

            var transformConfig = new TransformConfig {
                Shape = shapeConfig,
                Slope = slopeConfig,
                Flags = TransformFlags.None,
            };
            m_NtPathTransformToolSystem.SetTransformationConfig(transformConfig);
        }

        private void HandleUpdateShapeConfig(ShapeConfigData configData) {
            m_Log.Debug($"HandleUpdateShapeConfig(template: {configData.Template})");
            m_ShapeConfigBinding.Value = configData;
            var shapeConfig = configData.Template?.ToLowerInvariant() switch {
                "preserve" => ShapeCurveConfig.Preserve(),
                "straighten" => ShapeCurveConfig.Straighten(),
                "smooth" => ShapeCurveConfig.Smooth(configData.SmoothingFactor),
                _ => ShapeCurveConfig.Preserve(),
            };

            // Build transform config combining shape and slope
            var slopeData = m_SlopeConfigBinding.Value;
            var slopeConfig = slopeData.Template?.ToLowerInvariant() switch {
                "preserve" => SlopeCurveConfig.Preserve(),
                "linear" => SlopeCurveConfig.Linear(),
                "easeinout" => SlopeCurveConfig.EaseInOut(slopeData.EaseInLength, slopeData.EaseOutLength),
                "parabolic" => SlopeCurveConfig.Parabolic(slopeData.ArchHeight, slopeData.ArchPosition),
                _ => SlopeCurveConfig.Linear(),
            };

            var transformConfig = new TransformConfig {
                Shape = shapeConfig,
                Slope = slopeConfig,
                Flags = TransformFlags.None,
            };
            m_NtPathTransformToolSystem.SetTransformationConfig(transformConfig);
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
            
            m_NtPathTransformToolSystem.RequestApply();
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
                Template = "preserve",
                EaseInLength = 0.25f,
                EaseOutLength = 0.25f,
                ArchHeight = 0.5f,
                ArchPosition = 0.5f,
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

        /// <summary>
        /// Struct to store and synchronize shape configuration parameters with the UI.
        /// </summary>
        public struct ShapeConfigData : IJsonWritable, IJsonReadable {
            public string Template;
            public float SmoothingFactor;

            /// <summary>
            /// Creates default configuration with Preserve template.
            /// </summary>
            public static ShapeConfigData Default() => new() {
                Template = "preserve",
                SmoothingFactor = 0.5f,
            };

            /// <inheritdoc/>
            public void Write(IJsonWriter writer) {
                writer.TypeBegin(GetType().FullName);

                writer.PropertyName("template");
                writer.Write(Template);

                writer.PropertyName("smoothingFactor");
                writer.Write(SmoothingFactor);

                writer.TypeEnd();
            }

            /// <inheritdoc/>
            public void Read(IJsonReader reader) {
                reader.ReadMapBegin();

                if (reader.ReadProperty("template")) {
                    reader.Read(out string template);
                    Template = template;
                }

                if (reader.ReadProperty("smoothingFactor")) {
                    reader.Read(out float smoothingFactor);
                    SmoothingFactor = smoothingFactor;
                }

                reader.ReadMapEnd();
            }
        }
    }
}