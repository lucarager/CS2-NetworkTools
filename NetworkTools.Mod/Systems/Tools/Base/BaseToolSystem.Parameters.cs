namespace NetworkTools.Systems.Tools {
    using System.Collections.Generic;
    using NetworkTools.Settings;
    using NetworkTools.Systems.Tools.Handles;
    using NetworkTools.Systems.Tools.Parameters;

    using Newtonsoft.Json;
    using Unity.Entities;
    using Unity.Mathematics;

    public abstract partial class NT_BaseToolSystem {
        private ParameterBase[] m_ToolParameters;
        private Dictionary<string, ParameterBase> m_ParametersByKey;

        protected readonly struct HandleEntry {
            public ParameterBase Parameter { get; }
            public IHandleSpec   Spec      { get; }

            public HandleEntry(ParameterBase parameter, IHandleSpec spec) {
                Parameter = parameter;
                Spec      = spec;
            }
        }

        protected class ParentChildLink {
            public Float3Parameter Child;
            public float3          LastParentPos;
        }

        protected Dictionary<Entity, HandleEntry>                m_HandleEntries;
        protected Dictionary<ParameterBase, List<Entity>>        m_ParameterHandles;
        protected Dictionary<Float3Parameter, ParentChildLink[]> m_ParentChildLinks;

        /// <summary>
        ///     All <see cref="ParameterBase" /> fields declared on the concrete tool class, in declaration order.
        ///     Lazily discovered via reflection and cached per instance.
        ///     On first access, permanent subscribers are wired for every parameter.
        /// </summary>
        public IReadOnlyList<ParameterBase> Parameters {
            get {
                if (m_ToolParameters == null) {
                    m_ToolParameters = ParameterSchema.Discover(this);
                    WireParameterSubscribers();
                }
                return m_ToolParameters;
            }
        }

        /// <summary>
        ///     All parameters keyed by their <see cref="ParameterBase.Key" />.
        ///     Use this when handle generators need to resolve parameter references by key.
        /// </summary>
        public IReadOnlyDictionary<string, ParameterBase> ParametersByKey {
            get {
                if (m_ParametersByKey == null) {
                    m_ParametersByKey = new Dictionary<string, ParameterBase>(Parameters.Count);
                    foreach (var p in Parameters) m_ParametersByKey[p.Key] = p;
                }
                return m_ParametersByKey;
            }
        }

        /// <summary>Reset every parameter on this tool to its declared default.</summary>
        public void ResetAll() {
            foreach (var p in Parameters) p.ResetToDefault();
        }

        /// <summary>Reset a single parameter by key. Returns false if the key is not found.</summary>
        public bool Reset(string key) {
            foreach (var p in Parameters) {
                if (p.Key == key) {
                    p.ResetToDefault();
                    return true;
                }
            }
            return false;
        }

        private void WireParameterSubscribers() {
            m_HandleEntries    = new Dictionary<Entity, HandleEntry>();
            m_ParameterHandles = new Dictionary<ParameterBase, List<Entity>>();
            m_ParentChildLinks = new Dictionary<Float3Parameter, ParentChildLink[]>();

            foreach (var p in m_ToolParameters) {
                p.OnChanged += MarkUpdateNeeded;

                // Reverse-sync: when a parameter changes via code, update its active handle entities.
                // Skips Handle origin (the handle is already positioned by the drag).
                // No-op when m_ParameterHandles is empty (before RebuildHandlesForActiveMode).
                var param = p;
                param.OnChanged += origin => {
                    if (origin == ChangeOrigin.Handle) return;
                    if (!m_ParameterHandles.TryGetValue(param, out var entities)) return;
                    foreach (var entity in entities)
                        m_HandleEntries[entity].Spec.SyncToEntity(this, entity, param);
                };

                // Parent → child propagation: when a Float3Parameter moves, propagate to children.
                // Two concerns: (1) shift Float3Parameter child values by delta, and
                // (2) update circle/rotation handle entity centers that reference this parent.
                // Does not filter by origin — children follow regardless of how the parent changed.
                param.OnChanged += _ => {
                    if (param is not Float3Parameter pp) return;

                    if (m_ParentChildLinks.TryGetValue(pp, out var links)) {
                        foreach (var link in links) {
                            var delta = pp.Value - link.LastParentPos;
                            link.LastParentPos = pp.Value;
                            if (math.lengthsq(delta) < 1e-8f) continue;
                            link.Child.Value += delta;
                        }
                    }

                    SyncParentPositionToChildHandles(pp);
                };
            }
        }

        private void MarkUpdateNeeded(ChangeOrigin _) => m_UpdateNeeded = true;

        /// <summary>
        ///     Persists all <see cref="ParameterBase.Persist" /> parameters to
        ///     <see cref="NT_Settings.SavedParameterValues" />, keyed by <c>toolID.paramKey</c>.
        /// </summary>
        protected void SaveParameters() {
            var settings = NetworkToolsMod.Instance?.Settings;
            if (settings == null) return;

            var dict   = LoadParameterDictionary(settings);
            var prefix = toolID + ".";

            foreach (var p in Parameters) {
                if (!p.Persist) continue;
                var serialized = p.SerializeValue();
                if (serialized != null) dict[prefix + p.Key] = serialized;
            }

            settings.SavedParameterValues = JsonConvert.SerializeObject(dict);
            settings.ApplyAndSave();
        }

        /// <summary>
        ///     Restores persisted parameter values from <see cref="NT_Settings.SavedParameterValues" />.
        ///     Only parameters with <see cref="ParameterBase.Persist" /> set are restored.
        /// </summary>
        protected void RestoreParameters() {
            var settings = NetworkToolsMod.Instance?.Settings;
            if (settings == null) return;

            var dict   = LoadParameterDictionary(settings);
            var prefix = toolID + ".";

            foreach (var p in Parameters) {
                if (!p.Persist) continue;
                if (dict.TryGetValue(prefix + p.Key, out var raw)) p.TryDeserializeValue(raw);
            }
        }

        /// <summary>
        ///     Deserializes the saved parameter dictionary from settings, returning an empty dictionary on failure.
        /// </summary>
        private static Dictionary<string, string> LoadParameterDictionary(NT_Settings settings) {
            try {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(settings.SavedParameterValues ?? "{}")
                       ?? new Dictionary<string, string>();
            } catch {
                return new Dictionary<string, string>();
            }
        }
    }
}
