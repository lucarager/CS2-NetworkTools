namespace NetworkTools.Systems.Tools.Generate {
    using NetworkTools.Systems.Tools.Utils;

    using Unity.Collections;
    using Unity.Mathematics;

    public struct CircleGenerator : IGenerator {
        public static void Initialize(NT_GenerateToolSystem tool, float3 secondPos) {
            tool.CircleRadius.Value = math.clamp(
                math.distance(tool.Position.Value.xz, secondPos.xz),
                tool.CircleRadius.Min, tool.CircleRadius.Max);
        }

        public void Generate(
            in  GenerateJobConfig      config,
            ref NativeList<EdgeConfig> curves) {
            // A circle is an ellipse with equal radii. User-facing radius is inner-edge;
            // shift it out by half the prefab width to get the centerline radius.
            var radius = config.CircleRadius + config.NetWidth * 0.5f;
            OvalGenerator.GenerateEllipse(in config, radius, radius, ref curves);
        }
    }
}
