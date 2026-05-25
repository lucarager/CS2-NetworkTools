namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Mathematics;

    using Game.Tools;

    using NetworkTools.Systems.Tools.RoadShape;
    using NetworkTools.Systems.Tools.Utils;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    public struct OvalGenerator : IGenerator {
        private const int   Segments = 4;
        private const float Kappa    = 0.5522847498f; // 4 * (sqrt(2) - 1) / 3

        public void InitializeConfig(ref GenerateJobConfig config) {
        }

        public void Generate(
            in  GenerateJobConfig      config,
            ref NativeList<EdgeConfig> curves) {
            var radiusX    = config.OvalRadiusX + config.NetWidth * 0.5f;
            var radiusZ    = config.OvalRadiusZ + config.NetWidth * 0.5f;
            var yOffset    = config.Elevation + config.BaselineElevation;
            var center     = config.Position + new float3(0, yOffset, 0);
            var freeHeight = config.FollowTerrain || (yOffset > -config.ElevationLimit && yOffset < config.ElevationLimit)
                ? CoursePosFlags.FreeHeight
                : (CoursePosFlags)0;
            var nodeElevation = config.FollowTerrain ? yOffset : SlopeUtils.ClampElevation(yOffset);
            var rotation = config.StartDirection;

            float angleStep = 2f * math.PI / Segments;

            var nodes = new NativeArray<float3>(Segments, Allocator.Temp);
            for (int i = 0; i < Segments; i++) {
                float theta = i * angleStep;
                var local = new float3(radiusX * math.cos(theta), 0, radiusZ * math.sin(theta));
                nodes[i] = center + math.mul(rotation, local);
            }

            for (int i = 0; i < Segments; i++) {
                int next = (i + 1) % Segments;

                float theta0 = i * angleStep;
                float theta1 = next * angleStep;

                // Ellipse tangent: derivative of (rx*cos(t), 0, rz*sin(t))
                var t0 = new float3(-radiusX * math.sin(theta0), 0, radiusZ * math.cos(theta0));
                var t1 = new float3(-radiusX * math.sin(theta1), 0, radiusZ * math.cos(theta1));

                var p0Local = new float3(radiusX * math.cos(theta0), 0, radiusZ * math.sin(theta0));
                var p3Local = new float3(radiusX * math.cos(theta1), 0, radiusZ * math.sin(theta1));

                var p1 = center + math.mul(rotation, p0Local + Kappa * t0);
                var p2 = center + math.mul(rotation, p3Local - Kappa * t1);

                var bezier = new Bezier4x3(nodes[i], p1, p2, nodes[next]);
                curves.Add(new EdgeConfig {
                    StartNodePosition   = bezier.a,
                    EndNodePosition     = bezier.d,
                    Bezier              = bezier,
                    Length              = MathUtils.Length(bezier),
                    StartNodeElevation  = nodeElevation,
                    EndNodeElevation    = nodeElevation,
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
