namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Mathematics;

    using Game.Tools;

    using NetworkTools.Systems.Tools.Utils;

    using Unity.Collections;
    using Unity.Mathematics;

    public struct CircleGenerator : IGenerator {
        private const float PreviewDistance = 32f;

        public void InitializeConfig(ref GenerateJobConfig config) {
        }

        public void Generate(
            in  GenerateJobConfig      config,
            ref NativeList<EdgeConfig> curves) {

        }
    }
}
