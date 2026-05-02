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
                    return new GridGenerator().GetHandleDefinitions(jobConfig);
                case GenerateMode.Circle:
                    return null;
                default:
                    return System.Array.Empty<TransformHandleDefinition>();
            }
        }

        /// <inheritdoc />
        protected override void OnParameterHandleDragged(Entity handle, int key, float3 position, float value) {
            m_Log.Debug($"OnParameterHandleDragged: key={key}, value={value}");
        }

        /// <inheritdoc />
        protected override void OnPositionHandleDragged(Entity handle, int key, float3 position) {
            m_Log.Debug($"OnPositionHandleDragged: key={key}, position={position}");

            if (key == HandleKeys.StartPosition) {
                m_StartPosition = position;
            }
        }

        /// <inheritdoc />
        protected override float3 GetHandleConfigPosition(int key) {
            if (key == HandleKeys.StartPosition)
                return m_StartPosition;

            return float3.zero;
        }

        /// <inheritdoc />
        protected override void ApplyHandleConfigPosition(int key, float3 position) {
            if (key == HandleKeys.StartPosition) {
                m_StartPosition = position;
            }
        }
    }
}
