namespace NetworkTools.Systems.Tools.Generate {
    using NetworkTools.Systems.Tools.Base;
    using Unity.Collections;

    public interface IGenerator {
        /// <summary>
        ///     Called once when the template is selected or path changes.
        ///     Use to compute initial values that need to be stored in config
        ///     for both handle creation and transform execution.
        /// </summary>
        void InitializeConfig(in GenerateMode mode, ref GenerateConfig config);

        void GeneratePreview(
            in  GenerateMode         mode,
            in  GenerateConfig       config,
            ref NativeList<CurveDef> curves);

        void GenerateNetwork(
            in  GenerateMode         mode,
            in  GenerateConfig       config,
            ref NativeList<CurveDef> curves);
    }

    public interface IHandleableGenerator : IGenerator {
        TransformHandleDefinition[] GetHandleDefinitions(
            in GenerateMode   mode,
            in GenerateConfig config);
    }
}