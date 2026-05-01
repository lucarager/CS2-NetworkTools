namespace NetworkTools.Systems.Tools {
    using System.Collections.Generic;
    using NetworkTools.Systems.Parameters;

    public abstract partial class NT_BaseToolSystem {
        private ParameterBase[] m_ToolParameters;

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
