namespace NetworkTools.Systems.Tools.Generate {
    using NetworkTools.Systems.Tools.Base;

    using Unity.Collections;
    using Unity.Mathematics;

    public interface IGenerator {
        /// <summary>
        ///     Called once when the template is selected or path changes.
        ///     Use to compute initial values that need to be stored in config
        ///     for both handle creation and transform execution.
        /// </summary>
        void InitializeConfig(ref GenerateConfig config);

        void GeneratePreview(
            in float3 StartPosition,
            in quaternion StartDirection,
            ref NativeList<CurveDef> curves);

        void GenerateNetwork(
            in  GenerateConfig       config,
            ref NativeList<CurveDef> curves);
    }

    public interface IHandleableGenerator : IGenerator {
        TransformHandleDefinition[] GetHandleDefinitions(
            in GenerateConfig config);
    }
}