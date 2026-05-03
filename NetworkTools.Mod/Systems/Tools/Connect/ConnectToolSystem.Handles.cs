namespace NetworkTools.Systems.Tools.Connect {
    using NetworkTools.Systems.Tools.Base;
    using Unity.Entities;
    using Unity.Mathematics;

    public partial class NT_ConnectToolSystem {
        /// <summary>
        ///     Creates or refreshes handles based on the current mode and config.
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
                case ConnectMode.SimpleCurve:
                    return SimpleCurveGenerator.BuildHandleDefinitions(jobConfig, ParametersByKey);
                case ConnectMode.Loop:
                    return LoopGenerator.BuildHandleDefinitions(jobConfig, ParametersByKey);
                default:
                    return System.Array.Empty<TransformHandleDefinition>();
            }
        }
    }
}
