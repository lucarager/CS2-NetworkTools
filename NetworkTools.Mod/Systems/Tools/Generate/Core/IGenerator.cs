namespace NetworkTools.Systems.Tools.Generate {
    using NetworkTools.Systems.Tools.Utils;
    using Unity.Collections;
    using Unity.Mathematics;

    public interface IGenerator {
        /// <summary>
        ///     Called once when the template is selected or path changes.
        ///     Use to compute initial values that need to be stored in config
        ///     for both handle creation and transform execution.
        /// </summary>
        void InitializeConfig(ref GenerateJobConfig config);

        void GeneratePreview(
            in float3 StartPosition,
            in quaternion StartDirection,
            ref NativeList<EdgeConfig> curves);

        void GenerateNetwork(
            in  GenerateJobConfig      config,
            ref NativeList<EdgeConfig> curves);
    }
}
