namespace NetworkTools.Systems.Tools.Connect {
    using Colossal.Mathematics;

    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    public partial class NT_ConnectToolSystem {
#if BURST
        [BurstCompile]
#endif
        internal struct CreateDefinitionsJob : IJob {
            [ReadOnly] public required ConnectMode        Mode;
            [ReadOnly] public required ConnectConfig      Config;
            [ReadOnly] public required ToolOutputMode     OutputMode;
            [ReadOnly] public required NativeList<Entity> SelectedNodeEntities;
            [ReadOnly] public required Entity             PrefabEntity;

            [ReadOnly] public required ComponentLookup<Node>             NodeLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef>        PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge>       ConnectedEdgeLookup;
            [ReadOnly] public required ComponentLookup<Edge>             EdgeLookup;
            [ReadOnly] public required ComponentLookup<Curve>            CurveLookup;
            [ReadOnly] public required ComponentLookup<Upgraded>         UpgradedLookup;
            [ReadOnly] public required ComponentLookup<Aggregated>       AggregatedLookup;

            public required EntityCommandBuffer ECB;

            public void Execute() {
                // 1. Create data structures
                var curves = new NativeList<CurveDef>(64, Allocator.Temp);

                // 2. Create definitions
                switch (Mode) {
                    case ConnectMode.SimpleCurve:
                        new SimpleCurveGenerator().GenerateConnection(Mode, Config, ref curves);
                        break;
                    case ConnectMode.Loop:
                        new LoopGenerator().GenerateConnection(Mode, Config, ref curves);
                        break;
                }

                // 3. Output
                Output(curves);

                // Cleanup
                curves.Dispose();
            }

            private void Output(NativeList<CurveDef> curves) {
                if (OutputMode == ToolOutputMode.Preview) {
                    OutputPreview(curves);
                } else {
                    OutputApply(curves);
                }
            }

            private void OutputPreview(NativeList<CurveDef> curves) {
                // Output selected edges
                for (var i = 0; i < curves.Length; i++)
                {
                    var curve = curves[i];
                    OutputPreviewEdge(curve);
                }
            }

            private void OutputApply(NativeList<CurveDef> curves) {
            }

            private void OutputPreviewEdge(CurveDef curve) {
                var definitionEntity = ECB.CreateEntity();

                var creationDefinition = new CreationDefinition {
                    m_Original = Entity.Null,
                    m_Prefab = PrefabEntity,
                    m_Flags = CreationFlags.Recreate | CreationFlags.Parent
                };

                ECB.AddComponent(definitionEntity, creationDefinition);
                ECB.AddComponent<Updated>(definitionEntity);

                var startNodeFlags = CoursePosFlags.IsRight;
                var endNodeFlags = CoursePosFlags.IsRight;
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