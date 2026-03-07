namespace NetworkTools.Systems.Tools.Connect {
    using NetworkTools.Systems.Tools.Base;
    using Unity.Collections;

    public interface IConnectionGenerator {
        /// <summary>
        ///     Called once when the template is selected or path changes.
        ///     Use to compute initial values that need to be stored in config
        ///     for both handle creation and transform execution.
        /// </summary>
        void InitializeConfig(in ConnectMode mode, ref ConnectConfig config);

        void GenerateConnection(
            in  ConnectMode          mode,
            in  ConnectConfig        config,
            ref NativeList<CurveDef> curves);
    }

    public interface IHandleableConnectionGenerator : IConnectionGenerator {
        TransformHandleDefinition[] GetHandleDefinitions(
            in ConnectMode          mode,
            in ConnectConfig        config);
    }
}