namespace NetworkTools.Systems {
    #region Using Statements

    using Colossal.Entities;
    using Colossal.UI.Binding;
    using Game.Input;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI;
    using NetworkTools.Extensions;
    using NetworkTools.Settings;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.Connect;
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

        private ValueBindingHelper<int>                  m_AvailableSnapsBinding;
        private ValueBindingHelper<int>                  m_AvailableTargetsBinding;
        private ValueBindingHelper<int>                  m_AvailableViewsBinding;
        private ValueBindingHelper<int>                  m_ConnectModeBinding;
        private int                                      m_LastAvailableSnaps;
        private int                                      m_LastAvailableTargets;
        private int                                      m_LastAvailableViews;
        private int                                      m_LastConnectMode;
        private Entity                                   m_LastNetPrefabEntity;
        private int                                      m_LastSelectedNodesHash;
        private string                                   m_LastSelectedPrefab;
        private int                                      m_LastSelectedSnaps;
        private int                                      m_LastSelectedTargets;
        private int                                      m_LastSelectedViews;
        private int                                      m_LastToolPrefabCount;
        private PrefixedLogger                           m_Log;
        private NameSystem                               m_NameSystem;
        private NT_ConnectToolSystem                     m_NtConnectToolSystem;
        private NT_RoadShapeToolSystem                   m_NtRoadShapeToolSystem;
        private ValueBindingHelper<bool>                 m_PanelOpenBinding;
        private PrefabSystem                             m_PrefabSystem;
        private ValueBindingHelper<NetPrefabData>        m_SelectedNetPrefabBinding;
        private ValueBindingHelper<ToolSelectionData[]>  m_SelectedEntitiesBinding;
        private ValueBindingHelper<string>               m_SelectedPrefabBinding;
        private ValueBindingHelper<int>                  m_SelectedSnapsBinding;
        private ValueBindingHelper<int>                  m_SelectedTargetsBinding;
        private ValueBindingHelper<int>                  m_SelectedViewsBinding;
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
            m_NtConnectToolSystem   = World.GetOrCreateSystemManaged<NT_ConnectToolSystem>();
            m_NtRoadShapeToolSystem = World.GetOrCreateSystemManaged<NT_RoadShapeToolSystem>();
            m_NameSystem            = World.GetOrCreateSystemManaged<NameSystem>();

            m_ToolUIDataBinding       = CreateBinding("UI_DATA",             new NT_ToolPrefab[] { });
            m_SelectedPrefabBinding   = CreateBinding("SELECTED_PREFAB",     "");
            m_PanelOpenBinding        = CreateBinding("PANEL_OPEN",          false, HandlePanelOpen);
            m_SelectedEntitiesBinding = CreateBinding("SELECTED_ENTITIES",   new ToolSelectionData[] { });
            m_SelectedNetPrefabBinding = CreateBinding("SELECTED_NET_PREFAB", NetPrefabData.Empty);
            m_ShapeConfigBinding = CreateBinding("SHAPE_CONFIG",
                                                 ShapeTransformConfig.Preserve(),
                                                 HandleUpdateShapeConfig,
                                                 new ValueWriter<ShapeTransformConfig>(),
                                                 new ValueReader<ShapeTransformConfig>());
            m_ConnectModeBinding   = CreateBinding("CONNECT_MODE", (int)ConnectMode.None, HandleUpdateConnectMode);
            m_AvailableSnapsBinding   = CreateBinding("AVAILABLE_SNAPS",   (int)SnapOption.None);
            m_SelectedSnapsBinding    = CreateBinding("SELECTED_SNAPS",    (int)SnapOption.None, HandleUpdateSelectedSnaps);
            m_AvailableTargetsBinding = CreateBinding("AVAILABLE_TARGETS", (int)TargetOption.All);
            m_SelectedTargetsBinding  = CreateBinding("SELECTED_TARGETS",  (int)TargetOption.All, HandleUpdateSelectedTargets);
            m_AvailableViewsBinding   = CreateBinding("AVAILABLE_VIEWS",   (int)ViewOption.All);
            m_SelectedViewsBinding    = CreateBinding("SELECTED_VIEWS",    (int)ViewOption.None, HandleUpdateSelectedViews);

            CreateTrigger<string>("SELECT_TOOL", HandleSelectTool);
            CreateTrigger("APPLY_TRANSFORM", HandleApplyTransform);

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
            var selectedNodes    = m_ToolSystem.activeTool is INodeSelectionProvider selectionProvider
                                       ? selectionProvider.GetSelectedNodes()
                                       : System.Array.Empty<Entity>();
            var currentNodesHash = ComputeSelectionHash(selectedNodes);
            if (currentNodesHash != m_LastSelectedNodesHash) {
                m_LastSelectedNodesHash = currentNodesHash;
                var selectedEntitiesData = new ToolSelectionData[selectedNodes.Length];

                for (var i = 0; i < selectedNodes.Length; i++) {
                    var entity     = selectedNodes[i];
                    var entityType = DetermineEntityType(entity);
                    var entityName = entityType == SelectedEntityType.Node
                                         ? GetComputedNodeName(entity, i)
                                         : m_NameSystem.GetRenderedLabelName(entity);
                    selectedEntitiesData[i] = new ToolSelectionData(entity, entityType, entityName);
                }

                m_SelectedEntitiesBinding.Value = selectedEntitiesData;
            }

            // Update connect mode binding when the tool changes it
            var currentConnectMode = (int)m_NtConnectToolSystem.CurrentMode;
            if (currentConnectMode != m_LastConnectMode) {
                m_LastConnectMode          = currentConnectMode;
                m_ConnectModeBinding.Value = currentConnectMode;
            }

            // Update net prefab binding when the active tool's selection changes
            var netPrefabProvider  = m_ToolSystem.activeTool as INetPrefabSelectionProvider;
            var currentNetPrefabEntity = netPrefabProvider != null
                                             ? netPrefabProvider.SelectedNetPrefabEntity
                                             : Entity.Null;
            if (currentNetPrefabEntity != m_LastNetPrefabEntity) {
                m_LastNetPrefabEntity = currentNetPrefabEntity;
                var prefab            = netPrefabProvider?.SelectedNetPrefab;
                m_SelectedNetPrefabBinding.Value = prefab != null
                                                       ? new NetPrefabData(currentNetPrefabEntity,
                                                                           ImageSystem.GetThumbnail(prefab),
                                                                           prefab.name)
                                                       : NetPrefabData.Empty;
            }

            // Update snap/target bindings from the active tool
            var activeTool = m_ToolSystem.activeTool as NT_BaseToolSystem;

            var currentAvailableSnaps = activeTool != null ? (int)activeTool.AvailableSnaps : (int)SnapOption.None;
            if (currentAvailableSnaps != m_LastAvailableSnaps) {
                m_LastAvailableSnaps          = currentAvailableSnaps;
                m_AvailableSnapsBinding.Value = currentAvailableSnaps;
            }

            var currentSelectedSnaps = activeTool != null ? (int)activeTool.SelectedSnaps : (int)SnapOption.None;
            if (currentSelectedSnaps != m_LastSelectedSnaps) {
                m_LastSelectedSnaps          = currentSelectedSnaps;
                m_SelectedSnapsBinding.Value = currentSelectedSnaps;
            }

            var currentAvailableTargets = activeTool != null ? (int)activeTool.AvailableTargets : (int)TargetOption.All;
            if (currentAvailableTargets != m_LastAvailableTargets) {
                m_LastAvailableTargets          = currentAvailableTargets;
                m_AvailableTargetsBinding.Value = currentAvailableTargets;
            }

            var currentSelectedTargets = activeTool != null ? (int)activeTool.SelectedTargets : (int)TargetOption.All;
            if (currentSelectedTargets != m_LastSelectedTargets) {
                m_LastSelectedTargets          = currentSelectedTargets;
                m_SelectedTargetsBinding.Value = currentSelectedTargets;
            }

            var currentAvailableViews = activeTool != null ? (int)activeTool.AvailableViews : (int)ViewOption.All;
            if (currentAvailableViews != m_LastAvailableViews) {
                m_LastAvailableViews          = currentAvailableViews;
                m_AvailableViewsBinding.Value = currentAvailableViews;
            }

            var currentSelectedViews = activeTool != null ? (int)activeTool.SelectedViews : (int)ViewOption.None;
            if (currentSelectedViews != m_LastSelectedViews) {
                m_LastSelectedViews          = currentSelectedViews;
                m_SelectedViewsBinding.Value = currentSelectedViews;
            }

            if (m_ToggleToolPanelAction.WasPerformedThisFrame()) {
                m_PanelOpenBinding.Value = true;
            }

            base.OnUpdate();
        }

        private string GetComputedNodeName(Entity nodeEntity, int fallbackIndex) {
            if (TryGetNodeName(nodeEntity, out var streetName)) {
                return $"Node on {streetName}";
            }
            return $"Node {fallbackIndex + 1}";
        }

        private bool TryGetNodeName(Entity nodeEntity, out string name) {

            if (EntityManager.TryGetBuffer<ConnectedEdge>(nodeEntity, true, out var connectedEdges)) {
                // For now, get the first connected edge's name as the node name.
                // todo handle intersections.
                name = m_NameSystem.GetRenderedLabelName(connectedEdges[0].m_Edge);
                return true;
            }

            name = "Node";
            return false;
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

        private void HandleUpdateConnectMode(int mode) {
            m_Log.Debug($"HandleUpdateConnectMode(mode: {mode})");
            var connectMode = (ConnectMode)mode;
            m_NtConnectToolSystem.SetMode(connectMode);
            m_ConnectModeBinding.Value        = mode;
        }

        private void HandleApplyTransform() {
            m_NtRoadShapeToolSystem.RequestApply();
        }

        private void HandleUpdateSelectedSnaps(int value) {
            if (m_ToolSystem.activeTool is NT_BaseToolSystem activeTool) {
                activeTool.SelectedSnaps = (SnapOption)value;
            }
        }

        private void HandleUpdateSelectedTargets(int value) {
            if (m_ToolSystem.activeTool is NT_BaseToolSystem activeTool) {
                activeTool.SelectedTargets = (TargetOption)value;
                activeTool.RefreshEligibility();
            }
        }

        private void HandleUpdateSelectedViews(int value) {
            if (m_ToolSystem.activeTool is NT_BaseToolSystem activeTool) {
                activeTool.SelectedViews = (ViewOption)value;
                activeTool.RefreshViews();
            }
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

        /// <summary>
        ///     Struct to store and send selected net prefab data to the React UI.
        /// </summary>
        public readonly struct NetPrefabData : IJsonWritable {
            private readonly Entity m_Entity;
            private readonly string m_Thumbnail;
            private readonly string m_Name;

            public NetPrefabData(Entity entity, string thumbnail, string name) {
                m_Entity    = entity;
                m_Thumbnail = thumbnail;
                m_Name      = name;
            }

            /// <summary>
            ///     Returns a <see cref="NetPrefabData" /> representing no selection.
            /// </summary>
            public static NetPrefabData Empty => new(Entity.Null, "", "");

            /// <inheritdoc />
            public void Write(IJsonWriter writer) {
                writer.TypeBegin(GetType().FullName);

                writer.PropertyName("Entity");
                writer.Write(m_Entity);

                writer.PropertyName("Thumbnail");
                writer.Write(m_Thumbnail);

                writer.PropertyName("Name");
                writer.Write(m_Name);

                writer.TypeEnd();
            }
        }
    }
}