// <copyright file="NT_NodeSelectionToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license
// information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using System;
    using Colossal.Mathematics;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using NetworkTools.Systems.Tools.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    public partial class NT_AddNodeToolSystem {
        [Flags]
        public enum SnapMode {
            None = 0,
            Endpoints = 1 << 0, // Prevent snapping too close to edge endpoints
            Midpoint = 1 << 1, // Snap to curve midpoint
            Grid = 1 << 2 // Snap to grid increments
        }

        /// <summary>
        ///     Score multipliers for snap modes. Lower multiplier = higher priority.
        /// </summary>
        private static class SnapPriority {
            public const float Midpoint = 0.1f; // Highest priority - midpoint snaps are intentional
            public const float Grid = 0.5f; // Medium priority - grid is a soft preference
            public const float None = 1.0f; // Baseline - raw cursor position
        }

#if BURST
        [BurstCompile]
#endif
        private struct SnapControlPointJob : IJob {
            [ReadOnly] public required Entity EdgeEntity;
            [ReadOnly] public required float RawCurvePosition;
            [ReadOnly] public required ComponentLookup<Curve> CurveLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef> PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<NetGeometryData> NetGeometryDataLookup;
            [ReadOnly] public required SnapMode SnapMode;
            [ReadOnly] public required float GridSize;
            public required NativeReference<float> SnappedCurvePosition;
            public required NativeReference<float3> SnappedHitPosition;

            public void Execute() {
                if (!CurveLookup.TryGetComponent(EdgeEntity, out var curve) ||
                    !PrefabRefLookup.TryGetComponent(EdgeEntity, out var prefabRef) ||
                    !NetGeometryDataLookup.TryGetComponent(prefabRef.m_Prefab, out var netGeometry)) {
                    SnappedCurvePosition.Value = RawCurvePosition;
                    return;
                }

                // Calculate endpoint constraints (hard limit, not scored)
                var minCurvePosition = 0f;
                var maxCurvePosition = 1f;
                if ((SnapMode & SnapMode.Endpoints) != 0) {
                    minCurvePosition = NT_EdgeUtils.GetMinimumSplitDistance(curve.m_Length,
                        netGeometry.m_DefaultWidth,
                        netGeometry.m_EdgeLengthRange.min);
                    maxCurvePosition = 1f - minCurvePosition;
                }

                // Start with clamped raw position as baseline
                var clampedRaw = math.clamp(RawCurvePosition, minCurvePosition, maxCurvePosition);
                var bestPosition = clampedRaw;
                var bestScore = float.MaxValue;

                // Evaluate: No snap (baseline)
                EvaluateCandidate(clampedRaw, clampedRaw, SnapPriority.None, ref bestPosition, ref bestScore);


                // Evaluate: Midpoint snap
                if ((SnapMode & SnapMode.Midpoint) != 0) {
                    var midpoint = 0.5f;
                    if (midpoint >= minCurvePosition && midpoint <= maxCurvePosition) {
                        EvaluateCandidate(midpoint, clampedRaw, SnapPriority.Midpoint, ref bestPosition, ref bestScore);
                    }
                }

                // Evaluate: Grid snap
                if ((SnapMode & SnapMode.Grid) != 0) {
                    var positionAlongCurve = clampedRaw * curve.m_Length;
                    var snappedWorldPos = math.round(positionAlongCurve / GridSize) * GridSize;
                    var gridPosition = math.clamp(snappedWorldPos / curve.m_Length, minCurvePosition, maxCurvePosition);
                    EvaluateCandidate(gridPosition, clampedRaw, SnapPriority.Grid, ref bestPosition, ref bestScore);
                }

                SnappedCurvePosition.Value = bestPosition;
                SnappedHitPosition.Value   = MathUtils.Position(curve.m_Bezier, bestPosition);
            }

            /// <summary>
            ///     Evaluates a snap candidate and updates best match if it wins.
            ///     Score = distance from original × priority multiplier. Lower wins.
            /// </summary>
            private static void EvaluateCandidate(
                float candidate,
                float original,
                float priorityMultiplier,
                ref float bestPosition,
                ref float bestScore) {
                var distance = math.abs(candidate - original);
                var score = distance * priorityMultiplier;

                if (score < bestScore) {
                    bestScore    = score;
                    bestPosition = candidate;
                }
            }
        }


#if BURST
        [BurstCompile]
#endif
        private struct CreateDefinitionJob : IJob {
            [ReadOnly] public required Entity EdgeEntity;
            [ReadOnly] public required float3 HitPosition;
            [ReadOnly] public required float CurvePosition;
            [ReadOnly] public required ComponentLookup<Node> NodeLookup;
            [ReadOnly] public required ComponentLookup<Curve> CurveLookup;
            [ReadOnly] public required ComponentLookup<Edge> EdgeLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef> PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge> ConnectedEdgeLookup;
            [ReadOnly] public required TerrainHeightData TerrainHeight;
            [ReadOnly] public required OverlayRenderSystem.Buffer RenderBuffer;
            public required ToolOutputMode OutputMode;
            public required EntityCommandBuffer ECB;

            public void Execute() {
                // Validate we have an edge to work with
                if (EdgeEntity == Entity.Null) {
                    return;
                }

                // Get edge and curve data
                if (!EdgeLookup.TryGetComponent(EdgeEntity, out var edge)) {
                    return;
                }

                if (!CurveLookup.TryGetComponent(EdgeEntity, out var curve)) {
                    return;
                }

                if (!PrefabRefLookup.TryGetComponent(EdgeEntity, out var prefabRef)) {
                    return;
                }

                if (!PseudoRandomSeedLookup.TryGetComponent(EdgeEntity, out var seed)) {
                    return;
                }

                // For now, no difference between preview and apply - both create the same definition entities
                if (OutputMode == ToolOutputMode.Preview) {
                    OutputPreview(edge, curve, prefabRef, seed);
                }
                else {
                    OutputApply(edge, curve, prefabRef, seed);
                }
            }

            private void OutputPreview(Edge edge, Curve curve, PrefabRef prefabRef, PseudoRandomSeed seed) {
                var definitionEntity = ECB.CreateEntity();

                var creationDefinition = new CreationDefinition {
                    m_Original = Entity.Null,
                    m_Flags    = CreationFlags.Recreate
                };

                if (prefabRef.m_Prefab != Entity.Null) {
                    creationDefinition.m_Prefab = prefabRef;
                }

                creationDefinition.m_RandomSeed = seed.m_Seed;

                ECB.AddComponent(definitionEntity, creationDefinition);
                ECB.AddComponent<Updated>(definitionEntity);

                var netCourse = new NetCourse {
                    m_Curve      = new Bezier4x3(HitPosition, HitPosition, HitPosition, HitPosition),
                    m_Length     = 0,
                    m_FixedIndex = -1,
                    m_Elevation  = default,
                    m_StartPosition = new CoursePos {
                        m_Entity      = EdgeEntity,
                        m_Position    = HitPosition,
                        m_Rotation    = default,
                        m_CourseDelta = 0,
                        m_Elevation   = default,
                        m_Flags = CoursePosFlags.IsFirst | CoursePosFlags.IsLast | CoursePosFlags.IsRight |
                                  CoursePosFlags.IsLeft,
                        m_ParentMesh    = -1,
                        m_SplitPosition = CurvePosition
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity      = EdgeEntity,
                        m_Position    = HitPosition,
                        m_Rotation    = default,
                        m_CourseDelta = 1,
                        m_Elevation   = default,
                        m_Flags = CoursePosFlags.IsFirst | CoursePosFlags.IsLast | CoursePosFlags.IsRight |
                                  CoursePosFlags.IsLeft,
                        m_ParentMesh    = -1,
                        m_SplitPosition = CurvePosition
                    }
                };

                ECB.AddComponent(definitionEntity, netCourse);
            }


            private void OutputApply(Edge edge, Curve curve, PrefabRef prefabRef, PseudoRandomSeed seed) {
                OutputPreview(edge, curve, prefabRef, seed);
            }
        }
    }
}