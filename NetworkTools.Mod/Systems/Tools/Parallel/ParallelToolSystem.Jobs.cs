namespace NetworkTools.Systems.Tools.Parallel {
    using Colossal.Mathematics;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using NetworkTools.Systems.Tools.Utils;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    public partial class NT_ParallelToolSystem {
#if USE_BURST
        [BurstCompile]
#endif
        internal struct CreateDefinitionsJob : IJob {
            [ReadOnly] public required ToolOutputMode                    OutputMode;
            [ReadOnly] public required ParallelJobConfig                 Config;
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
            [ReadOnly] public required ComponentLookup<NetGeometryData> NetGeometryDataLookup;
            [ReadOnly] public required Entity                            NetPrefabEntity;
            [ReadOnly] public required Entity                            NetLanePrefabEntity;

            public required EntityCommandBuffer ECB;

            /// <summary>
            ///     Height thresholds that forces a road to be a tunnel
            /// </summary>
            public static readonly float2 TunnelThreshold = new(-11f, -11f);

            /// <summary>
            ///     Height thresholds that forces a road to be elevated.
            /// </summary>
            public static readonly float2 ElevatedThreshold = new(11f, 11f);

            public void Execute() {
                var signedDistance = Config.HorizontalOffset;
                var verticalOffset = Config.VerticalOffset;
                var verticalShift  = new float3(0f, verticalOffset, 0f);
                var edgeCount      = CurrentPathEdges.Length;

                if (edgeCount == 0) {
                    return;
                }

                // --- Phase 1: Collect path-ordered edge state and per-edge half-widths ---
                var edges      = new NativeArray<EdgeConfig>(edgeCount, Allocator.Temp);
                var halfWidths = new NativeArray<float>(edgeCount, Allocator.Temp);

                for (var i = 0; i < edgeCount; i++) {
                    var edgeEntity = CurrentPathEdges[i];
                    var edge       = EdgeLookup[edgeEntity];

                    if (!CurveLookup.TryGetComponent(edgeEntity, out var curve)) {
                        edges[i] = new EdgeConfig { IsValid = false };
                        continue;
                    }

                    var isForward = edge.m_Start == CurrentPathNodes[i];
                    var bezier    = isForward ? curve.m_Bezier : MathUtils.Invert(curve.m_Bezier);

                    edges[i] = new EdgeConfig {
                        EdgeEntity      = edgeEntity,
                        StartNodeEntity = isForward ? edge.m_Start : edge.m_End,
                        EndNodeEntity   = isForward ? edge.m_End : edge.m_Start,
                        IsForward       = isForward,
                        IsValid         = true,
                        Bezier          = bezier,
                        Length          = MathUtils.Length(bezier)
                    };

                    var halfWidth = 0f;
                    if (PrefabRefLookup.TryGetComponent(edgeEntity, out var prefabRef) &&
                        NetGeometryDataLookup.TryGetComponent(prefabRef.m_Prefab, out var netGeometry)) {
                        halfWidth = netGeometry.m_DefaultWidth * 0.5f;
                    }
                    halfWidths[i] = halfWidth;
                }

                // --- Phase 2: Compute offset node positions with miter at interior nodes ---
                // Interior nodes sit at the intersection of both adjacent offset lines,
                // preventing bezier overshoot / undershoot at sharp corners.
                var cachedNodePositions = new NativeHashMap<Entity, float3>(CurrentPathNodes.Length, Allocator.Temp);

                for (var i = 0; i < CurrentPathNodes.Length; i++) {
                    var nodeEntity = CurrentPathNodes[i];
                    if (cachedNodePositions.ContainsKey(nodeEntity)) {
                        continue;
                    }

                    var nodePos = NodeLookup[nodeEntity].m_Position;
                    var hasPrev = i > 0         && edges[i - 1].IsValid;
                    var hasNext = i < edgeCount && edges[i].IsValid;

                    float3 offset;

                    if (hasPrev && hasNext) {
                        var prevEffective = signedDistance + GetOriginShift(halfWidths[i - 1]);
                        var nextEffective = signedDistance + GetOriginShift(halfWidths[i]);
                        var effectiveDistance = (prevEffective + nextEffective) * 0.5f;
                        var prevBezier   = edges[i - 1].Bezier;
                        var nextBezier   = edges[i].Bezier;
                        var incomingPerp = GetPerpendicularOffset(prevBezier.c, prevBezier.d, 1f);
                        var outgoingPerp = GetPerpendicularOffset(nextBezier.a, nextBezier.b, 1f);
                        offset = ComputeMiterOffset(incomingPerp, outgoingPerp, effectiveDistance);
                    } else if (hasPrev) {
                        var effectiveDistance = signedDistance + GetOriginShift(halfWidths[i - 1]);
                        var prevBezier = edges[i - 1].Bezier;
                        offset = GetPerpendicularOffset(prevBezier.c, prevBezier.d, effectiveDistance);
                    } else if (hasNext) {
                        var effectiveDistance = signedDistance + GetOriginShift(halfWidths[i]);
                        var nextBezier = edges[i].Bezier;
                        offset = GetPerpendicularOffset(nextBezier.a, nextBezier.b, effectiveDistance);
                    } else {
                        offset = float3.zero;
                    }

                    cachedNodePositions.Add(nodeEntity, nodePos + offset + verticalShift);
                }

                // --- Phase 3: Build offset beziers and output definitions ---
                for (var i = 0; i < edgeCount; i++) {
                    var state = edges[i];
                    if (!state.IsValid) {
                        continue;
                    }

                    var offsetStartPos = cachedNodePositions[state.StartNodeEntity];
                    var offsetEndPos   = cachedNodePositions[state.EndNodeEntity];

                    // Tangent handles are invariant under uniform perpendicular translation,
                    // so (b − a) and (c − d) from the original bezier carry over directly.
                    var handleB = state.Bezier.b - state.Bezier.a;
                    var handleC = state.Bezier.c - state.Bezier.d;

                    // Assemble with miter-corrected endpoints
                    var offsetBezier = new Bezier4x3(offsetStartPos,
                                                     offsetStartPos + handleB,
                                                     offsetEndPos   + handleC,
                                                     offsetEndPos);

                    // Scale handles by the length ratio so curves preserve their roundness
                    if (state.Length > 0.001f) {
                        var newLength = MathUtils.Length(offsetBezier);
                        var scale     = newLength / state.Length;
                        offsetBezier = new Bezier4x3(offsetStartPos,
                                                     offsetStartPos + handleB * scale,
                                                     offsetEndPos   + handleC * scale,
                                                     offsetEndPos);
                    }

                    var startTangent  = math.normalize(MathUtils.StartTangent(offsetBezier));
                    var endTangent    = math.normalize(MathUtils.EndTangent(offsetBezier));
                    var startRotation = quaternion.LookRotationSafe(startTangent, math.up());
                    var endRotation   = quaternion.LookRotationSafe(endTangent,   math.up());
                    var offsetLength  = MathUtils.Length(offsetBezier);

                    var elevation = new float2(0f);

                    if (Config.VerticalOffset >= 0) {
                        elevation = ElevatedThreshold;
                    } else if (Config.VerticalOffset < 0) {
                        elevation = TunnelThreshold;
                    }

                    // Reverse direction if configured: swap start/end and reverse bezier
                    if (Config.ReverseDirection == ParallelDirection.Reverse) {
                        var reversedBezier        = new Bezier4x3(offsetBezier.d, offsetBezier.c, offsetBezier.b, offsetBezier.a);
                        var reversedStartTangent  = math.normalize(MathUtils.StartTangent(reversedBezier));
                        var reversedEndTangent    = math.normalize(MathUtils.EndTangent(reversedBezier));
                        var reversedStartRotation = quaternion.LookRotationSafe(reversedStartTangent, math.up());
                        var reversedEndRotation   = quaternion.LookRotationSafe(reversedEndTangent,   math.up());

                        OutputPreviewEdge(offsetEndPos,
                                          offsetStartPos,
                                          reversedStartRotation,
                                          reversedEndRotation,
                                          reversedBezier,
                                          offsetLength,
                                          elevation);
                    } else {
                        OutputPreviewEdge(offsetStartPos,
                                          offsetEndPos,
                                          startRotation,
                                          endRotation,
                                          offsetBezier,
                                          offsetLength,
                                          elevation);
                    }
                }

                edges.Dispose();
                halfWidths.Dispose();
                cachedNodePositions.Dispose();
            }

            private float GetOriginShift(float halfWidth) {
                switch (Config.Origin) {
                    case ParallelOrigin.LeftEdge:  return halfWidth;
                    case ParallelOrigin.RightEdge: return -halfWidth;
                    default:                       return 0f;
                }
            }

            /// <summary>
            ///     Computes the miter offset for a node where two path edges meet.
            ///     Places the node at the intersection of both perpendicular offset lines,
            ///     clamped to ≈4× the offset distance to avoid extreme spikes at very sharp angles.
            /// </summary>
            /// <param name="unitPerpIncoming">Unit perpendicular of the incoming edge's end tangent.</param>
            /// <param name="unitPerpOutgoing">Unit perpendicular of the outgoing edge's start tangent.</param>
            /// <param name="signedDistance">Signed offset distance (positive = right of travel).</param>
            private static float3 ComputeMiterOffset(float3 unitPerpIncoming, float3 unitPerpOutgoing,
                                                     float  signedDistance) {
                var miterDir = unitPerpIncoming + unitPerpOutgoing;
                var miterLen = math.length(miterDir);

                // Near-zero sum means edges are roughly anti-parallel (≈180° turn)
                if (miterLen < 0.001f) {
                    return unitPerpIncoming * signedDistance;
                }

                miterDir /= miterLen;

                // cos(halfAngle) determines how far along the miter direction we must
                // travel to reach both offset lines. Clamped so the miter never exceeds
                // ≈4× the offset distance (covers turns sharper than ≈150°).
                var cosHalfAngle = math.max(math.dot(miterDir, unitPerpIncoming), 0.25f);

                return miterDir * (signedDistance / cosHalfAngle);
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
                                           Bezier4x3  existingBezier,    float      existingLength, float2 elevation
            ) {
                var definitionEntity = ECB.CreateEntity();

                var creationDefinition = new CreationDefinition {
                    m_Original  = Entity.Null,
                    m_Prefab    = NetPrefabEntity,
                    m_SubPrefab = NetLanePrefabEntity,
                    m_Flags     = CreationFlags.SubElevation
                };

                ECB.AddComponent(definitionEntity, creationDefinition);
                ECB.AddComponent<Updated>(definitionEntity);

                var startNodeFlags = CoursePosFlags.IsLeft | CoursePosFlags.IsRight | CoursePosFlags.DisableMerge | CoursePosFlags.IsParallel;
                var endNodeFlags   = CoursePosFlags.IsLeft | CoursePosFlags.IsRight | CoursePosFlags.DisableMerge | CoursePosFlags.IsParallel;

                var startElevation  = elevation;
                var endElevation    = elevation;
                var courseElevation = elevation;


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