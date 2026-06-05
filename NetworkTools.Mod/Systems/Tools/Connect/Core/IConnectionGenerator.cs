namespace NetworkTools.Systems.Tools.Connect {
    using NetworkTools.Systems.Tools.Utils;
    using Unity.Collections;

    public interface IConnectionGenerator {
        void GenerateConnection(
            in  ConnectJobConfig      config,
            ref NativeList<EdgeConfig> curves);
    }
}
