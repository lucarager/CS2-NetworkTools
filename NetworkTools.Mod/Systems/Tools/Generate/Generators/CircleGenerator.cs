namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Mathematics;

    using Game.Tools;

    using NetworkTools.Systems.Tools.Utils;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    public struct CircleGenerator : IGenerator {
        private const int   Segments = 4;
        private const float Kappa    = 0.5522847498f; // 4 * (sqrt(2) - 1) / 3

        public void InitializeConfig(ref GenerateJobConfig config) {
        }

        public void Generate(
            in  GenerateJobConfig      config,
            ref NativeList<EdgeConfig> curves) {
            // User-facing radius is inner-edge; shift it out by half the prefab
            // width to get the centerline radius the geometry needs.
            var radius     = config.CircleRadius + config.NetWidth * 0.5f;
            var yOffset    = config.Elevation + config.BaselineElevation;
            var center     = config.Position + new float3(0, yOffset, 0);
            var freeHeight = config.FollowTerrain || (yOffset > -config.ElevationLimit && yOffset < config.ElevationLimit)
                ? CoursePosFlags.FreeHeight
                : (CoursePosFlags)0;
            var rotation = config.StartDirection;

            float angleStep = 2f * math.PI / Segments;

            // Pre-compute node positions so shared endpoints are bit-identical.
            var nodes = new NativeArray<float3>(Segments, Allocator.Temp);
            for (int i = 0; i < Segments; i++) {
                float theta = i * angleStep;
                var local = new float3(math.cos(theta), 0, math.sin(theta)) * radius;
                nodes[i] = center + math.mul(rotation, local);
            }

            for (int i = 0; i < Segments; i++) {
                int next = (i + 1) % Segments;

                float theta0 = i * angleStep;
                float theta1 = next * angleStep;

                var t0 = new float3(-math.sin(theta0), 0, math.cos(theta0));
                var t1 = new float3(-math.sin(theta1), 0, math.cos(theta1));

                var p0Local = new float3(math.cos(theta0), 0, math.sin(theta0)) * radius;
                var p3Local = new float3(math.cos(theta1), 0, math.sin(theta1)) * radius;

                var p1 = center + math.mul(rotation, p0Local + Kappa * radius * t0);
                var p2 = center + math.mul(rotation, p3Local - Kappa * radius * t1);

                var bezier = new Bezier4x3(nodes[i], p1, p2, nodes[next]);
                curves.Add(new EdgeConfig {
                    StartNodePosition   = bezier.a,
                    EndNodePosition     = bezier.d,
                    Bezier              = bezier,
                    Length              = MathUtils.Length(bezier),
                    StartNodeElevation  = yOffset,
                    EndNodeElevation    = yOffset,
                    NetPrefabEntity     = config.NetPrefabEntity,
                    NetLanePrefabEntity = config.NetLanePrefabEntity,
                    StartNodeFlags      = CoursePosFlags.IsRight | freeHeight,
                    EndNodeFlags        = CoursePosFlags.IsRight | freeHeight,
                });
            }

            nodes.Dispose();
        }
    }
}
