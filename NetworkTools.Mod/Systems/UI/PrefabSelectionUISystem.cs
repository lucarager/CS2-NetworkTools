namespace NetworkTools.Systems.UI {
    using System.Linq;
    using Colossal.Entities;
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

        private ValueBindingHelper<PrefabSelectionEntry[]> m_CachedPrefabBinding;
        private ValueBindingHelper<PrefabSelectionEntry[]> m_DataBinding;

        private PrefabBase                m_DefaultRoadPrefab;
        private Entity                    m_LastCachedEntity;
        private int                       m_LastPrefabType = -1;
        private PrefixedLogger            m_Log;
        private NT_PrefabCacheToolSystem  m_PrefabCacheSystem;
        private PrefabSystem              m_PrefabSystem;
        private ToolSystem                m_ToolSystem;
        private ValueBindingHelper<int>   m_TypeBinding;

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(NT_PrefabSelectionUISystem));
            m_Log.Debug("OnCreate()");

            m_PrefabSystem      = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_PrefabCacheSystem = World.GetOrCreateSystemManaged<NT_PrefabCacheToolSystem>();
            m_ToolSystem        = World.GetOrCreateSystemManaged<ToolSystem>();

            m_TypeBinding          = CreateBinding("PS:SELECTED_TYPE", (int)PrefabType.Road, HandleUpdateType);
            m_DataBinding          = CreateBinding("PS:DATA",          new PrefabSelectionEntry[] { });
            m_CachedPrefabBinding  = CreateBinding("PS:CACHED_PREFAB", new PrefabSelectionEntry[] { });
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

            UpdateCachedPrefabBinding();

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
            for (int i = 0; i < entities.Length; i++) {
                if (m_PrefabSystem.TryGetPrefab<PrefabBase>(entities[i], out var prefab)) {
                    m_DefaultRoadPrefab = prefab;
                    break;
                }
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
                if (m_PrefabSystem.TryGetPrefab<PrefabBase>(entity, out var prefab)) {
                    tool.SetNetPrefab(key, prefab);
                }
            }
        }

        private PrefabSelectionEntry[] GetRoadPrefabs() {
            var entities = SystemAPI.QueryBuilder().WithAll<RoadData>().Build().ToEntityArray(Allocator.Temp);
            return GetSortedPrefabs(entities, PrefabType.Road);
        }

        private PrefabSelectionEntry[] GetPathPrefabs() {
            var entities = SystemAPI.QueryBuilder().WithAll<PathwayData>().Build().ToEntityArray(Allocator.Temp);
            return GetSortedPrefabs(entities, PrefabType.Path);
        }

        private PrefabSelectionEntry[] GetRailPrefabs() {
            var entities = SystemAPI.QueryBuilder().WithAll<TrackData>().Build().ToEntityArray(Allocator.Temp);
            return GetSortedPrefabs(entities, PrefabType.Rail);
        }

        private PrefabSelectionEntry[] GetWaterwayPrefabs() {
            var entities = SystemAPI.QueryBuilder().WithAll<WaterwayData>().Build().ToEntityArray(Allocator.Temp);
            return GetSortedPrefabs(entities, PrefabType.Waterway);
        }

        private PrefabSelectionEntry[] GetNetLanePrefabs() {
            var entities = SystemAPI.QueryBuilder().WithAll<NetLaneData>().Build().ToEntityArray(Allocator.Temp);
            return GetSortedPrefabs(entities, PrefabType.NetLane);
        }

        private PrefabSelectionEntry[] GetSortedPrefabs(NativeArray<Entity> entities, PrefabType type) {
            return entities.Select(entity => {
                               if (!m_PrefabSystem.TryGetPrefab<PrefabBase>(entity, out var prefab))
                                   return ((PrefabSelectionEntry entry, int groupPriority, int itemPriority)?)null;
                               var name   = prefab.name;
                               var icon   = ImageSystem.GetThumbnail(prefab);
                               var (groupPriority, itemPriority) = GetPriority(entity);
                               return (entry: new PrefabSelectionEntry(entity, name, icon, type), groupPriority, itemPriority);
                           })
                           .Where(x => x.HasValue)
                           .Select(x => x!.Value)
                           .OrderBy(x => x.groupPriority)
                           .ThenBy(x => x.itemPriority)
                           .Select(x => x.entry)
                           .ToArray();
        }

        private void UpdateCachedPrefabBinding() {
            var cache = m_PrefabCacheSystem.LastNetPrefab;
            if (!cache.HasSelection) {
                if (m_LastCachedEntity != Entity.Null) {
                    m_LastCachedEntity = Entity.Null;
                    m_CachedPrefabBinding.Value = new PrefabSelectionEntry[] { };
                }
                return;
            }

            var isLane = cache.Prefab is NetLanePrefab;
            var entity = isLane ? cache.NetLanePrefabEntity : cache.NetPrefabEntity;

            if (entity == m_LastCachedEntity) return;
            m_LastCachedEntity = entity;

            var prefab = cache.Prefab;
            var type = prefab switch {
                PathwayPrefab  => PrefabType.Path,
                TrackPrefab    => PrefabType.Rail,
                WaterwayPrefab => PrefabType.Waterway,
                NetLanePrefab  => PrefabType.NetLane,
                _              => PrefabType.Road
            };

            m_CachedPrefabBinding.Value = new[] {
                new PrefabSelectionEntry(entity, prefab.name, ImageSystem.GetThumbnail(prefab), type)
            };
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