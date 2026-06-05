namespace NetworkTools.Systems.Tools.Generate {
    using NetworkTools.Systems.Tools.Utils;
    using Unity.Collections;

    public interface IGenerator {
        void Generate(
            in  GenerateJobConfig      config,
            ref NativeList<EdgeConfig> curves);
    }
}
