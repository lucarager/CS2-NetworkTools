namespace NetworkTools.Systems.Tools {
    using Colossal.Mathematics;
    using Game.Common;
    using Game.Net;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    ///     Job definitions for <see cref="NT_GridToolSystem" />.
    /// </summary>
    public partial class NT_GridToolSystem {
#if BURST
        [BurstCompile]
#endif
        internal struct CreateDefinitionsJob : IJob {
            [ReadOnly] public required GridConfig     Config;
            [ReadOnly] public required ToolOutputMode OutputMode;
            [ReadOnly] public required Entity         NetPrefabEntity;
            [ReadOnly] public required Entity         NetLanePrefabEntity;

            public required EntityCommandBuffer ECB;

            public void Execute() {
                // Derive rotated direction vectors from the config angle.
                var angleRad = math.radians(Config.Angle);
                var xDir     = new float3(math.cos(angleRad),  0f, math.sin(angleRad));
                var zDir     = new float3(-math.sin(angleRad), 0f, math.cos(angleRad));

                var origin = Config.StartPosition;
                var xCount = Config.XNum + 1; // node count along X
                var zCount = Config.ZNum + 1; // node count along Z

                // Precompute all grid node positions once so that every segment
                // referencing the same intersection gets a bit-identical float3.
                var nodes = new NativeArray<float3>(xCount * zCount, Allocator.Temp);
                for (var j = 0; j < zCount; j++) {
                    for (var i = 0; i < xCount; i++) {
                        nodes[j * xCount + i] = origin
                                               + xDir * (i * Config.XSpacing)
                                               + zDir * (j * Config.ZSpacing);
                    }
                }

                // X-direction segments: for each row j, emit segments (i,j) → (i+1,j).
                for (var j = 0; j < zCount; j++) {
                    var isParallel = j != 0;
                    for (var i = 0; i < Config.XNum; i++) {
                        var startPos = nodes[j * xCount + i];
                        var endPos   = nodes[j * xCount + i + 1];
                        OutputStraightEdge(startPos, endPos, isParallel);
                    }
                }

                // Z-direction segments: for each column i, emit segments (i,j) → (i,j+1).
                for (var i = 0; i < xCount; i++) {
                    var isParallel = i != 0;
                    for (var j = 0; j < Config.ZNum; j++) {
                        var startPos = nodes[j       * xCount + i];
                        var endPos   = nodes[(j + 1) * xCount + i];
                        OutputStraightEdge(startPos, endPos, isParallel);
                    }
                }

                nodes.Dispose();
            }

            private void OutputStraightEdge(float3 startPos, float3 endPos, bool isParallel) {
                var length = math.distance(startPos, endPos);
                if (length < math.EPSILON) {
                    return;
                }

                var bezier = new Bezier4x3(startPos,
                                           math.lerp(startPos, endPos, 1f / 3f),
                                           math.lerp(startPos, endPos, 2f / 3f),
                                           endPos);

                OutputPreviewEdge(startPos, endPos, bezier, length, default, isParallel);
            }


            private void OutputPreviewEdge(float3    startNodePosition, float3 endNodePosition,
                                           
                                           Bezier4x3 bezier,            float  length, float elevation, bool isParallel
            ) {
                var definitionEntity = ECB.CreateEntity();

                var creationDefinition = new CreationDefinition {
                    m_Original  = Entity.Null,
                    m_Prefab    = NetPrefabEntity,
                    m_SubPrefab = NetLanePrefabEntity,
                    m_Flags     = CreationFlags.Construction
                };

                ECB.AddComponent(definitionEntity, creationDefinition);
                ECB.AddComponent<Updated>(definitionEntity);

                var startNodeFlags  = CoursePosFlags.IsFirst | CoursePosFlags.IsRight | CoursePosFlags.FreeHeight | CoursePosFlags.IsGrid;
                var endNodeFlags    = CoursePosFlags.IsLast | CoursePosFlags.IsRight | CoursePosFlags.FreeHeight | CoursePosFlags.IsGrid;


                if (isParallel) {
                    startNodeFlags |= CoursePosFlags.IsParallel;
                    endNodeFlags   |= CoursePosFlags.IsParallel;
                }

                var startElevation  = new float2(elevation);
                var endElevation    = new float2(elevation);
                var courseElevation = new float2(elevation);

                var netCourse = new NetCourse {
                    m_Curve      = bezier,
                    m_Length     = length,
                    m_FixedIndex = -1,
                    m_Elevation  = courseElevation,
                    m_StartPosition = new CoursePos {
                        m_Entity        = Entity.Null,
                        m_Position      = startNodePosition,
                        m_Rotation      = NetUtils.GetNodeRotation(MathUtils.StartTangent(bezier)),
                        m_CourseDelta   = 0,
                        m_Elevation     = startElevation,
                        m_Flags         = startNodeFlags,
                        m_ParentMesh    = -1,
                        m_SplitPosition = 0
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity        = Entity.Null,
                        m_Position      = endNodePosition,
                        m_Rotation      = NetUtils.GetNodeRotation(MathUtils.EndTangent(bezier)),
                        m_CourseDelta   = 1,
                        m_Elevation     = endElevation,
                        m_Flags         = endNodeFlags,
                        m_ParentMesh    = -1,
                        m_SplitPosition = 0
                    }
                };

                ECB.AddComponent(definitionEntity, netCourse);
            }
        }
    }
}