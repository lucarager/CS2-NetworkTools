namespace NetworkTools.Systems.Tools.Generate {
    using NetworkTools.Systems.Tools.Base;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Handle management for <see cref="NT_GenerateToolSystem"/>.
    /// </summary>
    public partial class NT_GenerateToolSystem {
        /// <summary>
        ///     Creates or refreshes handles based on the current config.
        /// </summary>
        private void RefreshTransformHandles() {
            DestroyAllHandles();

            m_Log.Debug("RefreshTransformHandles: Creating handles");

            var handleDefs = GetHandleDefinitions();
            CreateHandlesFromDefinitions(handleDefs);
        }

        /// <summary>
        ///     Gets handle definitions for the current mode.
        /// </summary>
        private TransformHandleDefinition[] GetHandleDefinitions() {
            var jobConfig = BuildJobConfig();

            switch (Mode.Value) {
                case GenerateMode.Grid:
                    return GridGenerator.BuildHandleDefinitions(jobConfig, ParametersByKey);
                case GenerateMode.Circle:
                    return null;
                default:
                    return System.Array.Empty<TransformHandleDefinition>();
            }
        }
    }
}
