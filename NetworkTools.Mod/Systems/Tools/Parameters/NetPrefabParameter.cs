namespace NetworkTools.Systems.Tools.Parameters {
    using Game.Prefabs;
    using Unity.Entities;

    public class NetPrefabParameter : ParameterBase {
        public Entity NetPrefabEntity { get; private set; }
        public Entity NetLanePrefabEntity { get; private set; }
        public PrefabBase Prefab { get; private set; }

        public bool HasSelection => Prefab != null;

        /// <summary>Whether this prefab selection can be cleared to "None" in the UI.</summary>
        public bool Nullable { get; }

        public NetPrefabParameter(string key, int modes = 0, bool nullable = false, string label = null)
            : base(key, modes, bindable: false, label: label, persist: false) {
            Nullable = nullable;
        }

        public void Set(PrefabBase prefab, Entity netPrefabEntity, Entity netLanePrefabEntity) {
            if (NetPrefabEntity == netPrefabEntity && NetLanePrefabEntity == netLanePrefabEntity) return;
            Prefab = prefab;
            NetPrefabEntity = netPrefabEntity;
            NetLanePrefabEntity = netLanePrefabEntity;
            Log?.Debug($"[Parameter] {Key}: → {prefab?.name ?? "null"}");
            RaiseChanged();
        }

        public override void ResetToDefault() {
            Log?.Debug($"[Parameter] {Key}: reset to default (null)");
            Prefab = null;
            NetPrefabEntity = Entity.Null;
            NetLanePrefabEntity = Entity.Null;
            RaiseChanged();
        }
    }
}
