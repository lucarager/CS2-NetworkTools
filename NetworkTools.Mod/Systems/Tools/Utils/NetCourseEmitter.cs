namespace NetworkTools.Systems.Tools.Utils {
    using Colossal.Mathematics;
    using Game.Common;
    using Game.Net;
    using Game.Tools;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Shared builder for the <c>CreationDefinition</c> + two-<c>CoursePos</c> <c>NetCourse</c>
    ///     preview entity emitted by the create-style tools (Generate, Connect, Parallel).
    ///     Each tool fully populates an <see cref="EdgeConfig"/> — geometry, node identity,
    ///     rotations, flags, elevations, prefab overrides — and this emitter assembles the temp
    ///     entity. Edit-style tools (RoadShape, SuperNode) that mutate existing curves keep their
    ///     own output paths, but for the Tunnel mode of RoadShape.
    /// </summary>
    internal static class NetCourseEmitter {
        /// <summary>
        ///     Creates a temp definition entity (<c>CreationDefinition</c> + <c>Updated</c> +
        ///     <c>NetCourse</c>) describing a single new edge.
        /// </summary>
        public static void EmitPreview(ref EntityCommandBuffer ecb, in EdgeConfig e,
                                       CreationFlags flags, Entity original = default) {
            var definitionEntity = ecb.CreateEntity();

            ecb.AddComponent(definitionEntity, new CreationDefinition {
                m_Original   = original,
                m_Prefab     = e.NetPrefabEntity,
                m_SubPrefab  = e.NetLanePrefabEntity,
                m_RandomSeed = e.RandomSeed,
                m_Flags      = flags,
            });
            ecb.AddComponent<Updated>(definitionEntity);

            ecb.AddComponent(definitionEntity, new NetCourse {
                m_Curve      = e.Bezier,
                m_Length     = e.Length,
                m_FixedIndex = -1,
                m_Elevation  = e.CourseElevation,
                m_StartPosition = new CoursePos {
                    m_Entity        = e.StartNodeEntity,
                    m_Position      = e.StartNodePosition,
                    m_Rotation      = e.StartNodeRotation,
                    m_CourseDelta   = 0,
                    m_Elevation     = new float2(e.StartNodeElevation),
                    m_Flags         = e.StartNodeFlags,
                    m_ParentMesh    = -1,
                    m_SplitPosition = 0,
                },
                m_EndPosition = new CoursePos {
                    m_Entity        = e.EndNodeEntity,
                    m_Position      = e.EndNodePosition,
                    m_Rotation      = e.EndNodeRotation,
                    m_CourseDelta   = 1,
                    m_Elevation     = new float2(e.EndNodeElevation),
                    m_Flags         = e.EndNodeFlags,
                    m_ParentMesh    = -1,
                    m_SplitPosition = 0,
                },
            });

            // Without an original, the game takes the edge's upgrades from its definition.
            if (e.Upgrades != default) {
                ecb.AddComponent(definitionEntity, new Upgraded {
                    m_Flags = e.Upgrades,
                });
            }
        }

        /// <summary>
        ///     Creates a temp definition entity that splits the existing edge under a position.
        ///     It is a course of no length, which the game ends with a node like any other.
        /// </summary>
        /// <param name="ecb">Command buffer to create the definition with.</param>
        /// <param name="prefab">Prefab of the edge to split.</param>
        /// <param name="randomSeed">Random seed of the edge.</param>
        /// <param name="position">Position of the split, on the edge's curve.</param>
        /// <param name="curvePosition">Curve position of the split on the edge.</param>
        public static void EmitSplit(
            ref EntityCommandBuffer ecb,
            Entity                  prefab,
            int                     randomSeed,
            float3                  position,
            float                   curvePosition) {
            var definitionEntity = ecb.CreateEntity();

            ecb.AddComponent(definitionEntity, new CreationDefinition {
                m_Prefab     = prefab,
                m_RandomSeed = randomSeed,
                m_Flags      = CreationFlags.Construction,
            });
            ecb.AddComponent<Updated>(definitionEntity);

            var coursePos = new CoursePos {
                m_Position      = position,
                m_Flags         = CoursePosFlags.IsFirst
                                  | CoursePosFlags.IsLast
                                  | CoursePosFlags.IsRight
                                  | CoursePosFlags.IsLeft,
                m_ParentMesh    = -1,
                m_SplitPosition = curvePosition,
            };
            var netCourse = new NetCourse {
                m_Curve         = new Bezier4x3(position, position, position, position),
                m_FixedIndex    = -1,
                m_StartPosition = coursePos,
                m_EndPosition   = coursePos,
            };

            netCourse.m_EndPosition.m_CourseDelta = 1;
            ecb.AddComponent(definitionEntity, netCourse);
        }
    }
}
