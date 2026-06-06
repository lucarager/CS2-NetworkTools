namespace NetworkTools.Systems.Tools.Connect {
    using Colossal.Mathematics;

    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;

    using NetworkTools.Systems.Tools.Utils;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    public partial class NT_ConnectToolSystem {
#if USE_BURST
        [BurstCompile]
#endif
        internal struct CreateDefinitionsJob : IJob {
            [ReadOnly] public required ConnectMode        Mode;
            [ReadOnly] public required ConnectJobConfig   Config;
            [ReadOnly] public required ToolOutputMode     OutputMode;
            [ReadOnly] public required NativeList<Entity> SelectedNodeEntities;
            [ReadOnly] public required Entity             NetPrefabEntity;
            [ReadOnly] public required Entity             NetLanePrefabEntity;

            [ReadOnly] public required ComponentLookup<Node>             NodeLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef>        PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge>       ConnectedEdgeLookup;
            [ReadOnly] public required ComponentLookup<Edge>             EdgeLookup;
            [ReadOnly] public required ComponentLookup<Curve>            CurveLookup;
            [ReadOnly] public required ComponentLookup<Upgraded>         UpgradedLookup;
            [ReadOnly] public required ComponentLookup<Aggregated>       AggregatedLookup;
            [ReadOnly] public required ComponentLookup<NetGeometryData>  NetGeometryDataLookup;

            public required EntityCommandBuffer ECB;

            public void Execute() {
                // 1. Create data structures
                var curves = new NativeList<EdgeConfig>(64, Allocator.Temp);

                // Resolve all runtime prefab data into a local config copy so generators
                // receive a self-contained snapshot without needing component lookups.
                var config = Config;
                config.NetPrefabEntity = NetPrefabEntity;
                config.NetLanePrefabEntity = NetLanePrefabEntity;
                if (NetGeometryDataLookup.TryGetComponent(NetPrefabEntity, out var netGeom))
                {
                    config.NetWidth = netGeom.m_DefaultWidth;
                    config.ElevationLimit = netGeom.m_ElevationLimit;
                }

                // 2. Create definitions
                switch (Mode) {
                    case ConnectMode.SimpleCurve:
                        new SimpleCurveGenerator().GenerateConnection(config, ref curves);
                        break;
                    case ConnectMode.ComplexCurve:
                        new ComplexCurveGenerator().GenerateConnection(config, ref curves);
                        break;
                    case ConnectMode.Loop:
                        new LoopGenerator().GenerateConnection(config, ref curves);
                        break;
                }

                // 3. Output
                Output(curves);

                // Cleanup
                curves.Dispose();
            }

            private void Output(NativeList<EdgeConfig> curves) {
                // Output selected edges
                for (var i = 0; i < curves.Length; i++)
                {
                    var curve = curves[i];
                    OutputPreviewEdge(curve);
                }
            }

            private void OutputPreviewEdge(EdgeConfig EC) {
                var definitionEntity = ECB.CreateEntity();

                var creationDefinition = new CreationDefinition {
                    m_Original = Entity.Null,
                    m_Prefab = EC.NetPrefabEntity,
                    m_SubPrefab = EC.NetLanePrefabEntity,
                    m_Flags = CreationFlags.SubElevation,
                };

                ECB.AddComponent(definitionEntity, creationDefinition);
                ECB.AddComponent<Updated>(definitionEntity);

                var netCourse = new NetCourse {
                    m_Curve = EC.Bezier,
                    m_Length = EC.Length,
                    m_FixedIndex = -1,
                    m_StartPosition = new CoursePos {
                        m_Entity = EC.StartNodeEntity,
                        m_Position = EC.Bezier.a,
                        m_Rotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(EC.Bezier)),
                        m_CourseDelta = 0,
                        m_Elevation = EC.StartNodeElevation,
                        m_Flags = EC.StartNodeFlags,
                        m_ParentMesh = -1,
                        m_SplitPosition = 0
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity = EC.EndNodeEntity,
                        m_Position = EC.Bezier.d,
                        m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(EC.Bezier)),
                        m_CourseDelta = 1,
                        m_Elevation = EC.EndNodeElevation,
                        m_Flags = EC.EndNodeFlags,
                        m_ParentMesh = -1,
                        m_SplitPosition = 0
                    }
                };

                ECB.AddComponent(definitionEntity, netCourse);
            }
        }
    }
}
