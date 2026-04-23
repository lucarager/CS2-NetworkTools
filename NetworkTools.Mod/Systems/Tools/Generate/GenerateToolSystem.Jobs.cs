namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Mathematics;

    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;

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
            [ReadOnly] public required GenerateMode   Mode;
            [ReadOnly] public required GenerateConfig Config;
            [ReadOnly] public required ToolOutputMode OutputMode;
            [ReadOnly] public required Entity         NetPrefabEntity;
            [ReadOnly] public required Entity         NetLanePrefabEntity;
            [ReadOnly] public required bool           IsHoverPreview;
            [ReadOnly] public required ControlPoint   ControlPoint;

            [ReadOnly] public required ComponentLookup<Node> NodeLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef> PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge> ConnectedEdgeLookup;
            [ReadOnly] public required ComponentLookup<Edge> EdgeLookup;
            [ReadOnly] public required ComponentLookup<Curve> CurveLookup;
            [ReadOnly] public required ComponentLookup<Upgraded> UpgradedLookup;
            [ReadOnly] public required ComponentLookup<Aggregated> AggregatedLookup;

            public required EntityCommandBuffer ECB;

            public void Execute() {
                // 1. Create data structures
                var curves = new NativeList<CurveDef>(64, Allocator.Temp);

                // 2. Create definitions
                switch (Mode)
                {
                    case GenerateMode.Grid:
                        if (IsHoverPreview) {
                            new GridGenerator().GeneratePreview(ControlPoint.m_Position, ControlPoint.m_Rotation, ref curves);
                        } else {
                            new GridGenerator().GenerateNetwork(Config, ref curves);
                        }
                        break;
                    case GenerateMode.Circle:
                        break;
                }

                // 3. Output
                Output(curves);

                // Cleanup
                curves.Dispose();

                //// Derive rotated direction vectors from the config angle.
                //var angleRad = math.radians(Config.Angle);
                //var xDir     = new float3(math.cos(angleRad),  0f, math.sin(angleRad));
                //var zDir     = new float3(-math.sin(angleRad), 0f, math.cos(angleRad));

                //var origin = Config.StartPosition;
                //var xCount = Config.XNum + 1; // node count along X
                //var zCount = Config.ZNum + 1; // node count along Z

                //// Precompute all grid node positions once so that every segment
                //// referencing the same intersection gets a bit-identical float3.
                //var nodes = new NativeArray<float3>(xCount * zCount, Allocator.Temp);
                //for (var j = 0; j < zCount; j++) {
                //    for (var i = 0; i < xCount; i++) {
                //        nodes[j * xCount + i] = origin
                //                               + xDir * (i * Config.XSpacing)
                //                               + zDir * (j * Config.ZSpacing);
                //    }
                //}

                //// X-direction segments: for each row j, emit segments (i,j) → (i+1,j).
                //for (var j = 0; j < zCount; j++) {
                //    var isParallel = j != 0;
                //    for (var i = 0; i < Config.XNum; i++) {
                //        var startPos = nodes[j * xCount + i];
                //        var endPos   = nodes[j * xCount + i + 1];
                //        OutputStraightEdge(startPos, endPos, isParallel);
                //    }
                //}

                //// Z-direction segments: for each column i, emit segments (i,j) → (i,j+1).
                //for (var i = 0; i < xCount; i++) {
                //    var isParallel = i != 0;
                //    for (var j = 0; j < Config.ZNum; j++) {
                //        var startPos = nodes[j       * xCount + i];
                //        var endPos   = nodes[(j + 1) * xCount + i];
                //        OutputStraightEdge(startPos, endPos, isParallel);
                //    }
                //}

                //nodes.Dispose();
            }

            //private void OutputStraightEdge(float3 startPos, float3 endPos, bool isParallel) {
            //    var length = math.distance(startPos, endPos);
            //    if (length < math.EPSILON) {
            //        return;
            //    }

            //    var bezier = new Bezier4x3(startPos,
            //                               math.lerp(startPos, endPos, 1f / 3f),
            //                               math.lerp(startPos, endPos, 2f / 3f),
            //                               endPos);

            //    OutputPreviewEdge(startPos, endPos, bezier, length, default, isParallel);
            //}


            private void Output(NativeList<CurveDef> curves) {
                // Output selected edges
                for (var i = 0; i < curves.Length; i++)
                {
                    var curve = curves[i];
                    OutputPreviewEdge(curve);
                }
            }

            private void OutputPreviewEdge(CurveDef curve) {
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