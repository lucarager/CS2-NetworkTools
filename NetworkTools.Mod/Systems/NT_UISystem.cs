namespace NetworkTools.Systems {
    #region Using Statements

    using Colossal.UI.Binding;
    using Game.Input;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI;
    using NetworkTools.Extensions;
    using NetworkTools.Settings;
    using NetworkTools.Systems.Tools.RoadShape;
    using NetworkTools.Utils;
    using Unity.Collections;
    using Unity.Entities;

    #endregion

    /// <summary>
    ///     System responsible for UI Bindings & Lookup Handling.
    /// </summary>
    public partial class NT_UISystem : ExtendedUISystemBase {
        /// <summary>
        ///     Enum to represent the type of selected entity.
        /// </summary>
        public enum SelectedEntityType {
            Unknown = 0,
            Node    = 1,
            Edge    = 2
        }

        private int                                      m_LastSelectedNodesHash;
        private string                                   m_LastSelectedPrefab;
        private int                                      m_LastToolPrefabCount;
        private PrefixedLogger                           m_Log;
        private NameSystem                               m_NameSystem;
        private NT_RoadShapeToolSystem                   m_NtRoadShapeToolSystem;
        private ValueBindingHelper<bool>                 m_PanelOpenBinding;
        private PrefabSystem                             m_PrefabSystem;
        private ValueBindingHelper<ToolSelectionData[]>  m_SelectedEntitiesBinding;
        private ValueBindingHelper<string>               m_SelectedPrefabBinding;
        private ValueBindingHelper<ShapeTransformConfig> m_ShapeConfigBinding;
        private ProxyAction                              m_ToggleToolPanelAction;

        private EntityQuery                          m_ToolPrefabQuery;
        private ToolSystem                            m_ToolSystem;
        private ValueBindingHelper<NT_ToolPrefab[]>   m_ToolUIDataBinding;

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(NT_UISystem));
            m_Log.Debug("OnCreate()");

            m_PrefabSystem          = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ToolSystem            = World.GetOrCreateSystemManaged<ToolSystem>();
            m_NtRoadShapeToolSystem = World.GetOrCreateSystemManaged<NT_RoadShapeToolSystem>();
            m_NameSystem            = World.GetOrCreateSystemManaged<NameSystem>();

            m_ToolUIDataBinding       = CreateBinding("UI_DATA",           new NT_ToolPrefab[] { });
            m_SelectedPrefabBinding   = CreateBinding("SELECTED_PREFAB",   "");
            m_PanelOpenBinding        = CreateBinding("PANEL_OPEN",        false, HandlePanelOpen);
            m_SelectedEntitiesBinding = CreateBinding("SELECTED_ENTITIES", new ToolSelectionData[] { });
            m_ShapeConfigBinding = CreateBinding("SHAPE_CONFIG",
                                                 ShapeTransformConfig.Preserve(),
                                                 HandleUpdateShapeConfig,
                                                 new ValueWriter<ShapeTransformConfig>(),
                                                 new ValueReader<ShapeTransformConfig>());

            CreateTrigger<string>("SELECT_TOOL", HandleSelectTool);
            CreateTrigger("APPLY_SLOPE", HandleApplySlope);

            // Actions
            m_ToggleToolPanelAction = NetworkToolsMod.Instance.Settings.GetAction(NT_Settings.ToggleToolPanelStr);

            m_ToolPrefabQuery = SystemAPI.QueryBuilder()
                                         .WithAll<Components.NT_ToolData>()
                                         .Build();

            // Always enable
            m_ToggleToolPanelAction.shouldBeEnabled = true;
        }

        protected override void OnDestroy() {
            m_Log.Debug("OnDestroy()");
            m_ToggleToolPanelAction.shouldBeEnabled = false;
            base.OnDestroy();
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            // Update tool UI data when the prefab count changes
            var entityCount = m_ToolPrefabQuery.CalculateEntityCount();
            if (entityCount != m_LastToolPrefabCount) {
                m_LastToolPrefabCount = entityCount;
                var entities       = m_ToolPrefabQuery.ToEntityArray(Allocator.Temp);
                var toolPrefabArray = new NT_ToolPrefab[entities.Length];
                for (var i = 0; i < entities.Length; i++) {
                    toolPrefabArray[i] = m_PrefabSystem.GetPrefab<NT_ToolPrefab>(entities[i]);
                }

                m_ToolUIDataBinding.Value = toolPrefabArray;
            }

            // Update selected prefab binding when it changes
            var currentPrefab = m_ToolSystem.activePrefab != null
                                    ? m_ToolSystem.activePrefab.GetPrefabID().GetName()
                                    : "";
            if (currentPrefab != m_LastSelectedPrefab) {
                m_LastSelectedPrefab          = currentPrefab;
                m_SelectedPrefabBinding.Value = currentPrefab;
            }

            // Update selected entities binding when selection changes
            var selectedNodes    = m_NtRoadShapeToolSystem.GetSelectedNodes();
            var currentNodesHash = ComputeSelectionHash(selectedNodes);
            if (currentNodesHash != m_LastSelectedNodesHash) {
                m_LastSelectedNodesHash = currentNodesHash;
                var selectedEntitiesData = new ToolSelectionData[selectedNodes.Length];

                for (var i = 0; i < selectedNodes.Length; i++) {
                    var entity     = selectedNodes[i];
                    var entityType = DetermineEntityType(entity);
                    var entityName = entityType == SelectedEntityType.Node
                                         ? $"Node {i + 1}"
                                         : m_NameSystem.GetRenderedLabelName(entity);
                    selectedEntitiesData[i] = new ToolSelectionData(entity, entityType, entityName);
                }

                m_SelectedEntitiesBinding.Value = selectedEntitiesData;
            }

            if (m_ToggleToolPanelAction.WasPerformedThisFrame()) {
                m_PanelOpenBinding.Value = true;
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

        private void HandlePanelOpen(bool value) {
            m_Log.Debug($"HandlePanelOpen(value: {value})");
            m_PanelOpenBinding.Value = value;
        }

        private void HandleUpdateShapeConfig(ShapeTransformConfig configData) {
            m_Log.Debug($"HandleUpdateShapeConfig(template: {configData.Template})");


            var currentConfig = m_NtRoadShapeToolSystem.ShapeTransformConfig;
            if (currentConfig.Template == configData.Template) {
                m_ShapeConfigBinding.Value = configData;
                m_NtRoadShapeToolSystem.UpdateTransformationConfig(configData);
            } else {
                ShapeTransformConfig newConfig;

                // Create new config with default values
                switch (configData.Template) {
                    case ShapeTransformTemplate.Preserve:
                    default:
                        newConfig = ShapeTransformConfig.Preserve();
                        break;
                    case ShapeTransformTemplate.SlopeLinear:
                        newConfig = ShapeTransformConfig.SlopeLinear();
                        break;
                    case ShapeTransformTemplate.SlopeEaseInOut:
                        newConfig = ShapeTransformConfig.SlopeEaseInOut();
                        break;
                    case ShapeTransformTemplate.SlopeArch:
                        newConfig = ShapeTransformConfig.SlopeArch();
                        break;
                    case ShapeTransformTemplate.CurveStraighten:
                        newConfig = ShapeTransformConfig.CurveStraighten();
                        break;
                    case ShapeTransformTemplate.CurveSmooth:
                        newConfig = ShapeTransformConfig.CurveSmooth();
                        break;
                }

                m_NtRoadShapeToolSystem.SetTransformationConfig(newConfig);
                m_ShapeConfigBinding.Value = newConfig;
            }
        }

        private void HandleSelectTool(string id) {
            m_Log.Debug($"HandleSelectTool(id: {id})");

            if (m_PrefabSystem.TryGetPrefab(new PrefabID("NT_ToolPrefab",
                                                         id),
                                            out var prefab)) {
                m_ToolSystem.ActivatePrefabTool(prefab);
            }
        }

        private void HandleApplySlope() {
            m_NtRoadShapeToolSystem.RequestApply();
        }

        /// <summary>
        ///     Struct to store and send selected entity data to the React UI.
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

            /// <inheritdoc />
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