namespace NetworkTools.Systems.Tools.Generate {
    using NetworkTools.Systems.Tools.Utils;
    using Unity.Collections;

    public interface IGenerator {
        /// <summary>
        ///     Called once when the template is selected or path changes.
        ///     Use to compute initial values that need to be stored in config
        ///     for both handle creation and transform execution.
        /// </summary>
        void InitializeConfig(ref GenerateJobConfig config);

        void Generate(
            in  GenerateJobConfig      config,
            ref NativeList<EdgeConfig> curves);
    }
}
