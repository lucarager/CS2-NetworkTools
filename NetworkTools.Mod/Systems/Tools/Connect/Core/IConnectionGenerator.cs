namespace NetworkTools.Systems.Tools.Connect {
    using NetworkTools.Systems.Tools.Base;
    using Unity.Collections;

    public interface IConnectionGenerator {
        /// <summary>
        ///     Called once when the mode is selected or path changes.
        ///     Populates mode-specific fields in the config snapshot.
        /// </summary>
        void InitializeConfig(ref ConnectJobConfig config);

        void GenerateConnection(
            in  ConnectJobConfig     config,
            ref NativeList<CurveDef> curves);
    }

    public interface IHandleableConnectionGenerator : IConnectionGenerator {
        TransformHandleDefinition[] GetHandleDefinitions(
            in ConnectJobConfig config);
    }
}
