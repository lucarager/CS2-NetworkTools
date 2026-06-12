namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Collections;
    using Colossal.Mathematics;

    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;
    using Game.Zones;
    using NetworkTools.Systems.Tools.Utils;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    ///     Job definitions for <see cref="NT_GenerateToolSystem" />.
    /// </summary>
    public partial class NT_GenerateToolSystem {
#if USE_BURST
        [BurstCompile]
#endif
        internal struct CreateDefinitionsJob : IJob {
            [ReadOnly] public required GenerateMode      Mode;
            [ReadOnly] public required GenerateJobConfig Config;
            [ReadOnly] public required ToolOutputMode    OutputMode;
            [ReadOnly] public required Entity            NetPrefabEntity;
            [ReadOnly] public required Entity            NetLanePrefabEntity;
            [ReadOnly] public required RandomSeed        Seed;

            [ReadOnly] public required ComponentLookup<Node> NodeLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef> PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge> ConnectedEdgeLookup;
            [ReadOnly] public required ComponentLookup<Edge> EdgeLookup;
            [ReadOnly] public required ComponentLookup<Curve> CurveLookup;
            [ReadOnly] public required ComponentLookup<Upgraded> UpgradedLookup;
            [ReadOnly] public required ComponentLookup<Aggregated> AggregatedLookup;
            [ReadOnly] public required ComponentLookup<NetGeometryData> NetGeometryDataLookup;

            public required EntityCommandBuffer ECB;

            public void Execute() {
                // 1. Create data structures
                var curves = new NativeList<EdgeConfig>(64, Allocator.Temp);

                // Resolve all runtime prefab data into a local config copy so generators
                // receive a self-contained snapshot without needing component lookups.
                var config = Config;
                config.NetPrefabEntity    = NetPrefabEntity;
                config.NetLanePrefabEntity = NetLanePrefabEntity;
                if (NetGeometryDataLookup.TryGetComponent(NetPrefabEntity, out var netGeom)) {
                    config.NetWidth       = netGeom.m_DefaultWidth;
                    config.ElevationLimit = netGeom.m_ElevationLimit;
                }
                config.AltNetPrefabXWidth = NetGeometryDataLookup.TryGetComponent(config.AltNetPrefabXEntity, out var altXGeom)
                    ? altXGeom.m_DefaultWidth : config.NetWidth;
                config.AltNetPrefabZWidth = NetGeometryDataLookup.TryGetComponent(config.AltNetPrefabZEntity, out var altZGeom)
                    ? altZGeom.m_DefaultWidth : config.NetWidth;

                // 2. Create definitions
                switch (Mode)
                {
                    case GenerateMode.Grid:
                        new GridGenerator().Generate(config, ref curves);
                        break;
                    case GenerateMode.Circle:
                        new CircleGenerator().Generate(config, ref curves);
                        break;
                    case GenerateMode.Oval:
                        new OvalGenerator().Generate(config, ref curves);
                        break;
                }

                // 3. Output
                Output(curves);

                // Cleanup
                curves.Dispose();
            }

            private void Output(NativeList<EdgeConfig> curves) {
                var random = Seed.GetRandom(0);
                for (var i = 0; i < curves.Length; i++)
                {
                    var curve = curves[i];
                    curve.RandomSeed        = random.NextInt();
                    curve.StartNodeRotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(curve.Bezier));
                    curve.EndNodeRotation   = NetUtils.GetNodeRotation(MathUtils.EndTangent(curve.Bezier));
                    NetCourseEmitter.EmitPreview(ref ECB, in curve, CreationFlags.SubElevation);
                }
            }
        }
    }
}
