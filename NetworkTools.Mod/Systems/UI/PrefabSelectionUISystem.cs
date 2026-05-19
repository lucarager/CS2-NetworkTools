namespace NetworkTools.Systems.UI {
    using System.Linq;
    using Colossal.UI.Binding;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI;
    using NetworkTools.Extensions;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.Parameters;
    using NetworkTools.Utils;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    ///     System responsible for UI Bindings & Lookup Handling.
    /// </summary>
    public partial class NT_PrefabSelectionUISystem : ExtendedUISystemBase {
        /// <summary>
        ///     Enum to represent the type of selected entity.
        /// </summary>
        public enum PrefabType {
            Road,
            Path,
            Rail,
            Waterway,
            NetLane
        }

        private ValueBindingHelper<PrefabSelectionEntry[]> m_DataBinding;

        private PrefabBase             m_DefaultRoadPrefab;
        private int                    m_LastPrefabType = -1;
        private PrefixedLogger         m_Log;
        private PrefabSystem           m_PrefabSystem;
        private ToolSystem             m_ToolSystem;
        private ValueBindingHelper<int> m_TypeBinding;

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(NT_PrefabSelectionUISystem));
            m_Log.Debug("OnCreate()");

            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_ToolSystem   = World.GetOrCreateSystemManaged<ToolSystem>();

            m_TypeBinding = CreateBinding("PS:SELECTED_TYPE", (int)PrefabType.Road, HandleUpdateType);
            m_DataBinding = CreateBinding("PS:DATA",          new PrefabSelectionEntry[] { });
            CreateTrigger<string, Entity>("PS:SELECT", HandleSelect);
        }

        protected override void OnDestroy() {
            m_Log.Debug("OnDestroy()");
            base.OnDestroy();
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            var prefabType = m_TypeBinding.Value;
            if (m_LastPrefabType != prefabType) {
                m_LastPrefabType = prefabType;

                // Retrieve new prefab data based on the selected type
                var prefabData = prefabType switch {
                    (int)PrefabType.Road     => GetRoadPrefabs(),
                    (int)PrefabType.Path     => GetPathPrefabs(),
                    (int)PrefabType.Rail     => GetRailPrefabs(),
                    (int)PrefabType.Waterway => GetWaterwayPrefabs(),
                    (int)PrefabType.NetLane  => GetNetLanePrefabs(),
                    _                        => new PrefabSelectionEntry[] { }
                };
                m_DataBinding.Value = prefabData;
            }

            if (m_ToolSystem.activeTool is NT_BaseToolSystem tool) {
                foreach (var param in tool.Parameters) {
                    if (param is NetPrefabParameter np && !np.HasSelection) {
                        var defaultPrefab = GetDefaultRoadPrefab();
                        if (defaultPrefab != null) {
                            tool.SetNetPrefab(np.Key, defaultPrefab);
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Returns the cached default road prefab, querying for it once on first access.
        /// </summary>
        private PrefabBase GetDefaultRoadPrefab() {
            if (m_DefaultRoadPrefab != null) {
                return m_DefaultRoadPrefab;
            }

            var entities = SystemAPI.QueryBuilder()
                                   .WithAll<RoadData>()
                                   .Build()
                                   .ToEntityArray(Allocator.Temp);
            if (entities.Length > 0) {
                m_DefaultRoadPrefab = m_PrefabSystem.GetPrefab<PrefabBase>(entities[0]);
            }

            return m_DefaultRoadPrefab;
        }

        private void HandleUpdateType(int type) {
            m_Log.Debug($"HandleUpdateType(mode: {type})");
            m_TypeBinding.Value = type;
        }

        private void HandleSelect(string key, Entity entity) {
            m_Log.Debug($"HandleSelect(key: {key}, entity: {entity})");

            if (m_ToolSystem.activeTool is NT_BaseToolSystem tool) {
                var prefab = m_PrefabSystem.GetPrefab<PrefabBase>(entity);
                if (prefab != null) {
                    tool.SetNetPrefab(key, prefab);
                }
            }
        }

        private PrefabSelectionEntry[] GetRoadPrefabs() {
            return GetSortedPrefabs<RoadData>(PrefabType.Road);
        }

        private PrefabSelectionEntry[] GetPathPrefabs() {
            return GetSortedPrefabs<PathwayData>(PrefabType.Path);
        }

        private PrefabSelectionEntry[] GetRailPrefabs() {
            return GetSortedPrefabs<TrackData>(PrefabType.Rail);
        }

        private PrefabSelectionEntry[] GetWaterwayPrefabs() {
            return GetSortedPrefabs<WaterwayData>(PrefabType.Waterway);
        }

        private PrefabSelectionEntry[] GetNetLanePrefabs() {
            return GetSortedPrefabs<NetLaneData>(PrefabType.NetLane);
        }

        private PrefabSelectionEntry[] GetSortedPrefabs<T>(PrefabType type) where T : unmanaged, IComponentData {
            var entities = SystemAPI.QueryBuilder()
                                    .WithAll<T>()
                                    .Build()
                                    .ToEntityArray(Allocator.Temp);

            return entities.Select(entity => {
                               var prefab = m_PrefabSystem.GetPrefab<PrefabBase>(entity);
                               var name   = prefab.name;
                               var icon   = ImageSystem.GetThumbnail(prefab);
                               var (groupPriority, itemPriority) = GetPriority(entity);
                               return (entry: new PrefabSelectionEntry(entity, name, icon, type), groupPriority, itemPriority);
                           })
                           .OrderBy(x => x.groupPriority)
                           .ThenBy(x => x.itemPriority)
                           .Select(x => x.entry)
                           .ToArray();
        }

        private (int groupPriority, int itemPriority) GetPriority(Entity entity) {
            if (!EntityManager.TryGetComponent<UIObjectData>(entity, out var uiData)) {
                return (int.MaxValue, int.MaxValue);
            }

            var itemPriority = uiData.m_Priority;

            if (uiData.m_Group != Entity.Null &&
                EntityManager.TryGetComponent<UIObjectData>(uiData.m_Group, out var groupUiData)) {
                return (groupUiData.m_Priority, itemPriority);
            }

            return (int.MaxValue, itemPriority);
        }

        /// <summary>
        ///     Struct to store and send entity data to the React UI.
        /// </summary>
        public readonly struct PrefabSelectionEntry : IJsonWritable {
            private readonly Entity     m_Entity;
            private readonly string     m_Name;
            private readonly string     m_Icon;
            private readonly PrefabType m_Type;

            public PrefabSelectionEntry(Entity entity, string name, string icon, PrefabType type) {
                m_Entity = entity;
                m_Name   = name;
                m_Icon   = icon;
                m_Type   = type;
            }

            /// <inheritdoc />
            public void Write(IJsonWriter writer) {
                writer.TypeBegin(GetType().FullName);

                writer.PropertyName("Entity");
                writer.Write(m_Entity);

                writer.PropertyName("Name");
                writer.Write(m_Name);

                writer.PropertyName("Icon");
                writer.Write(m_Icon);

                writer.PropertyName("Type");
                writer.Write((int)m_Type);

                writer.TypeEnd();
            }
        }
    }
}