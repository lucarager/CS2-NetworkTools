namespace NetworkTools.Systems.UI {
    using Colossal.UI.Binding;
    using Game.Input;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI;
    using NetworkTools.Extensions;
    using NetworkTools.Settings;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.Connect;
    using NetworkTools.Systems.Tools.Generate;
    using NetworkTools.Systems.Tools.Parallel;
    using NetworkTools.Systems.Tools.RoadShape;
    using NetworkTools.Utils;
    using Unity.Entities;

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
        private ValueBindingHelper<int>                  m_GenerateModeBinding;
        private int                                      m_LastAvailableSnaps;
        private int                                      m_LastAvailableTargets;
        private int                                      m_LastAvailableViews;
        private int                                      m_LastConnectMode;
        private int                                      m_LastGenerateMode;
        private Entity                                   m_LastNetPrefabEntity;
        private int                                      m_LastSelectedNodesHash;
        private string                                   m_LastSelectedPrefab;
        private int                                      m_LastSelectedSnaps;
        private int                                      m_LastSelectedTargets;
        private int                                      m_LastSelectedViews;
        private int                                      m_LastShapeConfigRevision;
        private int                                      m_LastToolPrefabCount;
        private PrefixedLogger                           m_Log;
        private NameSystem                               m_NameSystem;
        private NT_ConnectToolSystem                     m_NtConnectToolSystem;
        private NT_GenerateToolSystem                    m_NtGenerateToolSystem;
        private NT_ParallelToolSystem                    m_NtParallelToolSystem;
        private NT_RoadShapeToolSystem                   m_NtRoadShapeToolSystem;
        private ValueBindingHelper<ParallelConfig>       m_ParallelConfigBinding;
        private ValueBindingHelper<bool>                 m_PanelOpenBinding;
        private PrefabSystem                             m_PrefabSystem;
        private ValueBindingHelper<NetPrefabData>        m_SelectedNetPrefabBinding;
        private ValueBindingHelper<ToolSelectionData[]>  m_SelectedEntitiesBinding;
        private ValueBindingHelper<string>               m_SelectedPrefabBinding;
        private ValueBindingHelper<int>                  m_SelectedSnapsBinding;
        private ValueBindingHelper<int>                  m_SelectedTargetsBinding;
        private ValueBindingHelper<int>                  m_SelectedViewsBinding;
        private ValueBindingHelper<GenerateConfig>       m_GenerateConfigBinding;
        private ValueBindingHelper<ShapeTransformConfig> m_ShapeConfigBinding;
        private ProxyAction                              m_ToggleToolPanelAction;
        private ProxyAction                              m_OpenTool1Action;
        private ProxyAction                              m_OpenTool2Action;
        private ProxyAction                              m_OpenTool3Action;
        private ProxyAction                              m_OpenTool4Action;
        private ProxyAction                              m_OpenTool5Action;
        private ProxyAction                              m_OpenTool6Action;
        private ProxyAction                              m_OpenTool7Action;
        private ProxyAction                              m_OpenTool8Action;
        private ProxyAction                              m_OpenTool9Action;
        private ProxyAction                              m_ApplyTransformationAction;

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
            m_NtGenerateToolSystem  = World.GetOrCreateSystemManaged<NT_GenerateToolSystem>();
            m_NtParallelToolSystem  = World.GetOrCreateSystemManaged<NT_ParallelToolSystem>();
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
            m_GenerateConfigBinding = CreateBinding("GENERATE_CONFIG",
                                                 new GenerateConfig(),
                                                 HandleUpdateGenerateConfig,
                                                 new ValueWriter<GenerateConfig>(),
                                                 new ValueReader<GenerateConfig>());
            m_ParallelConfigBinding = CreateBinding("PARALLEL_CONFIG",
                                                    ParallelConfig.Default,
                                                    HandleUpdateParallelConfig,
                                                 new ValueWriter<ParallelConfig>(),
                                                 new ValueReader<ParallelConfig>());
            m_ConnectModeBinding      = CreateBinding("CONNECT_MODE", (int)ConnectMode.None, HandleUpdateConnectMode);
            m_AvailableSnapsBinding   = CreateBinding("AVAILABLE_SNAPS",   (int)SnapOption.None);
            m_SelectedSnapsBinding    = CreateBinding("SELECTED_SNAPS",    (int)SnapOption.None, HandleUpdateSelectedSnaps);
            m_AvailableTargetsBinding = CreateBinding("AVAILABLE_TARGETS", (int)TargetOption.All);
            m_SelectedTargetsBinding  = CreateBinding("SELECTED_TARGETS",  (int)TargetOption.All, HandleUpdateSelectedTargets);
            m_AvailableViewsBinding   = CreateBinding("AVAILABLE_VIEWS",   (int)ViewOption.All);
            m_SelectedViewsBinding    = CreateBinding("SELECTED_VIEWS",    (int)ViewOption.None, HandleUpdateSelectedViews);
            m_GenerateModeBinding     = CreateBinding("GENERATE_MODE", (int)GenerateMode.None, HandleUpdateGenerateMode);

            CreateTrigger<string>("SELECT_TOOL", HandleSelectTool);
            CreateTrigger("REQUEST_APPLY", HandleRequestApply);

            // Actions
            m_ToggleToolPanelAction = NetworkToolsMod.Instance.Settings.GetAction(NT_Settings.ToggleToolPanelStr);
            m_OpenTool1Action       = NetworkToolsMod.Instance.Settings.GetAction(NT_Settings.OpenTool1Str);
            m_OpenTool2Action       = NetworkToolsMod.Instance.Settings.GetAction(NT_Settings.OpenTool2Str);
            m_OpenTool3Action       = NetworkToolsMod.Instance.Settings.GetAction(NT_Settings.OpenTool3Str);
            m_OpenTool4Action       = NetworkToolsMod.Instance.Settings.GetAction(NT_Settings.OpenTool4Str);
            m_OpenTool5Action       = NetworkToolsMod.Instance.Settings.GetAction(NT_Settings.OpenTool5Str);
            m_OpenTool6Action       = NetworkToolsMod.Instance.Settings.GetAction(NT_Settings.OpenTool6Str);
            m_OpenTool7Action       = NetworkToolsMod.Instance.Settings.GetAction(NT_Settings.OpenTool7Str);
            m_OpenTool8Action       = NetworkToolsMod.Instance.Settings.GetAction(NT_Settings.OpenTool8Str);
            m_OpenTool9Action       = NetworkToolsMod.Instance.Settings.GetAction(NT_Settings.OpenTool9Str);
            m_ApplyTransformationAction = NetworkToolsMod.Instance.Settings.GetAction(NT_Settings.ApplyTransformationStr);

            m_ToolPrefabQuery = SystemAPI.QueryBuilder()
                                         .WithAll<Components.NT_ToolData>()
                                         .Build();

            // Always enable
            m_ToggleToolPanelAction.shouldBeEnabled = true;
            m_OpenTool1Action.shouldBeEnabled       = true;
            m_OpenTool2Action.shouldBeEnabled       = true;
            m_OpenTool3Action.shouldBeEnabled       = true;
            m_OpenTool4Action.shouldBeEnabled       = true;
            m_OpenTool5Action.shouldBeEnabled       = true;
            m_OpenTool6Action.shouldBeEnabled       = true;
            m_OpenTool7Action.shouldBeEnabled       = true;
            m_OpenTool8Action.shouldBeEnabled       = true;
            m_OpenTool9Action.shouldBeEnabled       = true;
            m_ApplyTransformationAction.shouldBeEnabled = true;
        }

        protected override void OnDestroy() {
            m_Log.Debug("OnDestroy()");
            m_ToggleToolPanelAction.shouldBeEnabled = false;
            m_OpenTool1Action.shouldBeEnabled       = false;
            m_OpenTool2Action.shouldBeEnabled       = false;
            m_OpenTool3Action.shouldBeEnabled       = false;
            m_OpenTool4Action.shouldBeEnabled       = false;
            m_OpenTool5Action.shouldBeEnabled       = false;
            m_OpenTool6Action.shouldBeEnabled       = false;
            m_OpenTool7Action.shouldBeEnabled       = false;
            m_OpenTool8Action.shouldBeEnabled       = false;
            m_OpenTool9Action.shouldBeEnabled       = false;
            m_ApplyTransformationAction.shouldBeEnabled = false;
            base.OnDestroy();
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