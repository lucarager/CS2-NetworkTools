namespace NetworkTools.Systems.Tools.Parallel {
    using Colossal.Mathematics;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    public partial class NT_ParallelToolSystem {
#if BURST
        [BurstCompile]
#endif
        internal struct CreateDefinitionsJob : IJob {
            [ReadOnly] public required ToolOutputMode                    OutputMode;
            [ReadOnly] public required ParallelConfig                    Config;
            [ReadOnly] public required NativeList<Entity>                CurrentPathNodes;
            [ReadOnly] public required NativeList<Entity>                CurrentPathEdges;
            [ReadOnly] public required ComponentLookup<Node>             NodeLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef>        PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge>       ConnectedEdgeLookup;
            [ReadOnly] public required ComponentLookup<Edge>             EdgeLookup;
            [ReadOnly] public required ComponentLookup<Curve>            CurveLookup;
            [ReadOnly] public required ComponentLookup<Upgraded>         UpgradedLookup;
            [ReadOnly] public required ComponentLookup<Aggregated>       AggregatedLookup;
            [ReadOnly] public required Entity                            PrefabEntity;

            public required EntityCommandBuffer ECB;

            public void Execute() {
                var signedDistance  = Config.SignedHorizontalOffset;
                var verticalOffset = Config.VerticalOffset;
                var verticalShift  = new float3(0f, verticalOffset, 0f);

                // Cache offset node positions so shared nodes between adjacent edges
                // have bit-identical values regardless of which edge computes them first
                var cachedNodePositions = new NativeHashMap<Entity, float3>(CurrentPathNodes.Length, Allocator.Temp);

                for (var i = 0; i < CurrentPathEdges.Length; i++) {
                    var edgeEntity = CurrentPathEdges[i];
                    var edge       = EdgeLookup[edgeEntity];

                    if (!CurveLookup.TryGetComponent(edgeEntity, out var curve)) {
                        continue;
                    }

                    var existingBezier = curve.m_Bezier;

                    // Offset bezier control points perpendicularly and vertically
                    var offsetBezier = OffsetBezier(existingBezier, signedDistance);
                    offsetBezier.a += verticalShift;
                    offsetBezier.b += verticalShift;
                    offsetBezier.c += verticalShift;
                    offsetBezier.d += verticalShift;

                    // Offset node positions from the Node component (separate from bezier endpoints).
                    // Cached per entity so that adjacent edges sharing a node get the same value.
                    if (!cachedNodePositions.TryGetValue(edge.m_Start, out var offsetStartPos)) {
                        var startNodePos = NodeLookup[edge.m_Start].m_Position;
                        var startOffset  = GetPerpendicularOffset(existingBezier.a, existingBezier.b, signedDistance);
                        offsetStartPos   = startNodePos + startOffset + verticalShift;
                        cachedNodePositions.Add(edge.m_Start, offsetStartPos);
                    }

                    if (!cachedNodePositions.TryGetValue(edge.m_End, out var offsetEndPos)) {
                        var endNodePos = NodeLookup[edge.m_End].m_Position;
                        var endOffset  = GetPerpendicularOffset(existingBezier.c, existingBezier.d, signedDistance);
                        offsetEndPos   = endNodePos + endOffset + verticalShift;
                        cachedNodePositions.Add(edge.m_End, offsetEndPos);
                    }

                    // Rotations derived per-edge from the offset bezier tangents
                    var startTangent  = math.normalize(MathUtils.StartTangent(offsetBezier));
                    var endTangent    = math.normalize(MathUtils.EndTangent(offsetBezier));
                    var startRotation = quaternion.LookRotationSafe(startTangent, math.up());
                    var endRotation   = quaternion.LookRotationSafe(endTangent, math.up());

                    var offsetLength = MathUtils.Length(offsetBezier);

                    OutputPreviewEdge(offsetStartPos,
                                      offsetEndPos,
                                      startRotation,
                                      endRotation,
                                      PrefabEntity,
                                      offsetBezier,
                                      offsetLength,
                                      verticalOffset);
                }

                cachedNodePositions.Dispose();
            }

            /// <summary>
            ///     Offsets a bezier curve perpendicularly by the given signed distance.
            ///     Positive = right of travel direction, Negative = left.
            /// </summary>
            private static Bezier4x3 OffsetBezier(Bezier4x3 bezier, float signedDistance) {
                var offsetA = GetPerpendicularOffset(bezier.a, bezier.b, signedDistance);
                var offsetB = GetPerpendicularOffset(bezier.a, bezier.b, signedDistance);
                var offsetC = GetPerpendicularOffset(bezier.c, bezier.d, signedDistance);
                var offsetD = GetPerpendicularOffset(bezier.c, bezier.d, signedDistance);

                return new Bezier4x3(
                    bezier.a + offsetA,
                    bezier.b + offsetB,
                    bezier.c + offsetC,
                    bezier.d + offsetD
                );
            }

            /// <summary>
            ///     Computes the perpendicular offset vector for a segment defined by two points.
            ///     The offset is computed in the XZ plane (horizontal), preserving Y.
            /// </summary>
            private static float3 GetPerpendicularOffset(float3 from, float3 to, float signedDistance) {
                var direction = to - from;
                direction.y = 0f;

                var length = math.length(direction);
                if (length < 0.001f) {
                    return float3.zero;
                }

                var normalized = direction / length;

                // Perpendicular in XZ plane: rotate 90° clockwise (right = positive)
                var perpendicular = new float3(normalized.z, 0f, -normalized.x);
                return perpendicular * signedDistance;
            }

            private void OutputPreviewEdge(float3     startNodePosition, float3     endNodePosition,
                                           quaternion startNodeRotation, quaternion endNodeRotation,
                                           Entity     prefabEntity,      Bezier4x3  existingBezier, float existingLength, float elevation
            ) {
                var definitionEntity = ECB.CreateEntity();

                var creationDefinition = new CreationDefinition {
                    m_Original = Entity.Null,
                    m_Prefab   = prefabEntity
                };

                ECB.AddComponent(definitionEntity, creationDefinition);
                ECB.AddComponent<Updated>(definitionEntity);

                var startNodeFlags  = CoursePosFlags.IsRight;
                var endNodeFlags    = CoursePosFlags.IsRight;
                var startElevation  = new float2(elevation);
                var endElevation    = new float2(elevation);
                var courseElevation = new float2(elevation);

                var netCourse = new NetCourse {
                    m_Curve      = existingBezier,
                    m_Length     = existingLength,
                    m_FixedIndex = -1,
                    m_Elevation  = courseElevation,
                    m_StartPosition = new CoursePos {
                        m_Entity        = Entity.Null,
                        m_Position      = startNodePosition,
                        m_Rotation      = startNodeRotation,
                        m_CourseDelta   = 0,
                        m_Elevation     = startElevation,
                        m_Flags         = startNodeFlags,
                        m_ParentMesh    = -1,
                        m_SplitPosition = 0
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity        = Entity.Null,
                        m_Position      = endNodePosition,
                        m_Rotation      = endNodeRotation,
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