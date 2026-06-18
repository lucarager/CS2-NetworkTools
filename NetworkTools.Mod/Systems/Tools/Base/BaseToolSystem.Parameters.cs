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

        /// <summary>
        ///     One declared dependency: a handle (owned by <see cref="Owner" />) that derives from a
        ///     source parameter. Keyed in <see cref="m_Dependencies" /> by the source parameter.
        /// </summary>
        protected sealed class DependencyLink {
            public Entity           Entity;        // the dependent handle entity
            public IHandleSpec      Spec;          // its spec (for OnDependencyChanged dispatch)
            public ParameterBase    Owner;         // the parameter the handle belongs to
            public DependencyUpdate Update;        // custom reaction, or null => spec-type default
            public float3           LastSourcePos; // remembered source value for delta-follow specs
        }

        protected Dictionary<Entity, HandleEntry>                  m_HandleEntries;
        protected Dictionary<ParameterBase, List<Entity>>          m_ParameterHandles;
        protected Dictionary<ParameterBase, List<DependencyLink>>  m_Dependencies;

        /// <summary>
        ///     Parameters already updated in the current propagation pass. Non-null only while a
        ///     pass is running; seeded with the root (user-changed) parameter. Bounds cascades and
        ///     cycles to one update per parameter per pass. See <see cref="PropagateDependencies" />.
        /// </summary>
        private HashSet<ParameterBase> m_DependencyPass;

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
            m_Dependencies     = new Dictionary<ParameterBase, List<DependencyLink>>();

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

                // Dependency propagation: when this parameter changes, update every handle that
                // declared it as a source. The origin is forwarded so custom (delegate) reactions
                // can fire on first-class edits but not on propagation's own bookkeeping writes.
                param.OnChanged += origin => PropagateDependencies(param, origin);
            }
        }

        /// <summary>
        ///     Propagates a parameter value-change to every handle that declared it as a dependency
        ///     source. Each entry runs its custom <see cref="DependencyUpdate" /> or, when bare, the
        ///     spec-type default (<see cref="IHandleSpec.OnDependencyChanged" />). Dependency-driven
        ///     writes re-enter this method (cascading to grandchildren); a per-pass visited set
        ///     seeded with the root parameter bounds cascades and cycles to one update per parameter.
        ///     <para>
        ///     Custom (delegate) reactions express a relationship to a first-class edit of the source
        ///     (a user drag or code seed), so they are skipped when the source itself changed via
        ///     <see cref="ChangeOrigin.Dependency" /> — i.e. it merely co-moved under another
        ///     dependency. The owner handles that co-movement through its own bare follow instead.
        ///     This is what keeps two mirrored control points from re-mirroring when their shared node
        ///     translates both of them. The skip happens before the visited-set add so the owner's
        ///     bare link still claims its once-per-pass slot. Bare follows cascade on any origin.
        ///     </para>
        /// </summary>
        private void PropagateDependencies(ParameterBase source, ChangeOrigin origin) {
            if (m_Dependencies == null || !m_Dependencies.TryGetValue(source, out var links)) return;

            var isRoot = m_DependencyPass == null;
            if (isRoot) m_DependencyPass = new HashSet<ParameterBase> { source };

            try {
                foreach (var link in links) {
                    // A custom reaction fires on edits, not on a source's dependency-driven co-move.
                    if (origin == ChangeOrigin.Dependency && link.Update != null) continue;

                    // Re-base the remembered source value and compute the delta (used by delta-follow
                    // specs; ignored by recenter/re-resolve specs). Only Float3 sources have a delta.
                    var delta = float3.zero;
                    if (source is Float3Parameter sp) {
                        delta              = sp.Value - link.LastSourcePos;
                        link.LastSourcePos = sp.Value;
                    }

                    // Visited guard: update each owner at most once per pass (cycle/diamond safety).
                    if (!m_DependencyPass.Add(link.Owner)) continue;

                    if (link.Update != null) {
                        link.Update(this, link.Owner, source);
                    } else {
                        link.Spec.OnDependencyChanged(this, link.Entity, link.Owner, (Float3Parameter)source, delta);
                    }
                }
            } finally {
                if (isRoot) m_DependencyPass = null;
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
