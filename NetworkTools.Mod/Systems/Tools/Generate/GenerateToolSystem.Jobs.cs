namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Mathematics;

    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using NetworkTools.Systems.Tools.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    ///     Job definitions for <see cref="NT_GenerateToolSystem" />.
    /// </summary>
    public partial class NT_GenerateToolSystem {
#if BURST
        [BurstCompile]
#endif
        internal struct CreateDefinitionsJob : IJob {
            [ReadOnly] public required GenerateMode      Mode;
            [ReadOnly] public required GenerateJobConfig Config;
            [ReadOnly] public required ToolOutputMode    OutputMode;
            [ReadOnly] public required Entity            NetPrefabEntity;
            [ReadOnly] public required Entity            NetLanePrefabEntity;

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

                // Resolve prefab width so user-facing spacing/radius can be
                // interpreted edge-to-edge instead of centerline-to-centerline.
                var netWidth = 0f;
                if (NetGeometryDataLookup.TryGetComponent(NetPrefabEntity, out var netGeometry)) {
                    netWidth = netGeometry.m_DefaultWidth;
                }

                // 2. Create definitions
                switch (Mode)
                {
                    case GenerateMode.Grid:
                        new GridGenerator().Generate(Config, netWidth, ref curves);
                        break;
                    case GenerateMode.Circle:
                        new CircleGenerator().Generate(Config, netWidth, ref curves);
                        break;
                }

                // 3. Output
                Output(curves);

                // Cleanup
                curves.Dispose();
            }

            private void Output(NativeList<EdgeConfig> curves) {
                for (var i = 0; i < curves.Length; i++)
                {
                    var curve = curves[i];
                    OutputPreviewEdge(curve);
                }
            }

            private void OutputPreviewEdge(EdgeConfig curve) {
                var definitionEntity = ECB.CreateEntity();

                var creationDefinition = new CreationDefinition {
                    m_Original = Entity.Null,
                    m_Prefab = NetPrefabEntity,
                    m_SubPrefab = NetLanePrefabEntity,
                    m_Flags = CreationFlags.Construction
                };

                ECB.AddComponent(definitionEntity, creationDefinition);
                ECB.AddComponent<Updated>(definitionEntity);

                var startNodeFlags = CoursePosFlags.IsRight | CoursePosFlags.FreeHeight;
                var endNodeFlags = CoursePosFlags.IsRight | CoursePosFlags.FreeHeight;
                var startElevation = float2.zero;
                var endElevation = float2.zero;
                var courseElevation = float2.zero;

                var netCourse = new NetCourse {
                    m_Curve = curve.Bezier,
                    m_Length = curve.Length,
                    m_FixedIndex = -1,
                    m_Elevation = courseElevation,
                    m_StartPosition = new CoursePos {
                        m_Entity = curve.StartNodeEntity,
                        m_Position = curve.Bezier.a,
                        m_Rotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(curve.Bezier)),
                        m_CourseDelta = 0,
                        m_Elevation = startElevation,
                        m_Flags = startNodeFlags,
                        m_ParentMesh = -1,
                        m_SplitPosition = 0
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity = curve.EndNodeEntity,
                        m_Position = curve.Bezier.d,
                        m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(curve.Bezier)),
                        m_CourseDelta = 1,
                        m_Elevation = endElevation,
                        m_Flags = endNodeFlags,
                        m_ParentMesh = -1,
                        m_SplitPosition = 0
                    }
                };

                ECB.AddComponent(definitionEntity, netCourse);
            }
        }
    }
}
