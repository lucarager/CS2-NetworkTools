namespace NetworkTools.Systems.Tools {
    using System.Collections.Generic;

    using NetworkTools.Systems.Tools.Parameters;

    public abstract partial class NT_BaseToolSystem {
        private ParameterBase[] m_ToolParameters;
        private Dictionary<string, ParameterBase> m_ParametersByKey;

        /// <summary>
        ///     All <see cref="ParameterBase" /> fields declared on the concrete tool class, in declaration order.
        ///     Lazily discovered via reflection and cached per instance.
        /// </summary>
        public IReadOnlyList<ParameterBase> Parameters {
            get {
                if (m_ToolParameters == null)
                    m_ToolParameters = ParameterSchema.Discover(this);
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
    }
}
