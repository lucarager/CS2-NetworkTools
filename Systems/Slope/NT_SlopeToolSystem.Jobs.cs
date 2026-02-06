// <copyright file="NT_NodeSelectionToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using System;
    using Colossal.Mathematics;
    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using Unity.Burst.Intrinsics;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using Color = UnityEngine.Color;

    #endregion

    public partial class NT_SlopeToolSystem {
#if BURST
        [BurstCompile]
#endif
            /// <summary>
            /// Creates definitions for Entities from query.
            /// </summary>
            private struct CreateDefinitionJob : IJob {
            [ReadOnly] public required NativeList<Entity>                SelectedNodes;
            [ReadOnly] public required NativeList<Entity>                CurrentPathEdges;
            [ReadOnly] public required NativeList<Entity>                CurrentPathNodes;
            [ReadOnly] public required ComponentLookup<Node>             NodeLookup;
            [ReadOnly] public required ComponentLookup<Curve>            CurveLookup;
            [ReadOnly] public required ComponentLookup<Edge>             EdgeLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef>        PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required SlopeCurveConfig                  CurveConfig;
            [ReadOnly] public required TerrainHeightData                 TerrainHeight;
            [ReadOnly] public required OverlayRenderSystem.Buffer        RenderBuffer;
            public required            EntityCommandBuffer               ECB;

            public void Execute() {
                var startNode     = SelectedNodes[0];
                var endNode       = SelectedNodes[^1];
                var startNodeInfo = NodeLookup[startNode];
                var endNodeInfo   = NodeLookup[endNode];
                var startHeight   = startNodeInfo.m_Position.y;
                var endHeight     = endNodeInfo.m_Position.y;
                var deltaHeight   = endHeight - startHeight;

                // === Phase 1: Gather edge metadata and calculate total path length ===
                var edgeCount   = CurrentPathEdges.Length;
                var edgeData    = new NativeArray<EdgeSlopeData>(edgeCount, Allocator.Temp);
                var totalLength = 0f;

                for (var i = 0; i < edgeCount; i++) {
                    var edgeEntity = CurrentPathEdges[i];
                    var data       = new EdgeSlopeData();

                    if (!EdgeLookup.TryGetComponent(edgeEntity, out var edge)) {
                        edgeData[i] = data;
                        continue;
                    }

                    // Determine if edge direction matches path direction
                    var currentNode = CurrentPathNodes[i];
                    data.IsForward = edge.m_Start == currentNode;

                    if (CurveLookup.TryGetComponent(edgeEntity, out var curve)) {
                        data.Length = curve.m_Length;

                        // Calculate control point ratios in path order
                        SlopeCalculator.CalculateControlPointRatios(
                            curve.m_Bezier,
                            data.Length,
                            data.IsForward,
                            out data.CtrlStartRatio,
                            out data.CtrlEndRatio);
                    }

                    edgeData[i] = data;
                    totalLength += data.Length;
                }

                if (totalLength <= 0f) {
                    edgeData.Dispose();
                    return;
                }

                // === Phase 2: Calculate and apply heights to path edges ===
                var cumulativeDistance = 0f;

                for (var i = 0; i < edgeCount; i++) {
                    var edgeEntity = CurrentPathEdges[i];
                    var data       = edgeData[i];

                    if (!CurveLookup.TryGetComponent(edgeEntity, out var curve)) {
                        cumulativeDistance += data.Length;
                        continue;
                    }

                    if (!EdgeLookup.TryGetComponent(edgeEntity, out var edge)) {
                        cumulativeDistance += data.Length;
                        continue;
                    }

                    // Calculate heights using shared utility
                    var heights = SlopeCalculator.CalculateEdgeHeights(
                        cumulativeDistance,
                        data.Length,
                        data.CtrlStartRatio,
                        data.CtrlEndRatio,
                        totalLength,
                        startHeight,
                        deltaHeight,
                        CurveConfig);

                    // Apply heights to bezier using shared utility
                    var adjustedBezier = SlopeCalculator.ApplyHeightsToBezier(curve.m_Bezier, heights, data.IsForward);

                    // Entity to hold definitions for this edge
                    var definitionEntity = ECB.CreateEntity();

                    // CreationDefinition
                    var creationDefinition = new CreationDefinition {
                        m_Original = edgeEntity,
                        m_Flags    = CreationFlags.Recreate | CreationFlags.Parent,
                    };

                    if (PrefabRefLookup.HasComponent(edgeEntity)) {
                        creationDefinition.m_Prefab = new PrefabRef(PrefabRefLookup[edgeEntity].m_Prefab);
                    }

                    if (PseudoRandomSeedLookup.HasComponent(edgeEntity)) {
                        creationDefinition.m_RandomSeed = PseudoRandomSeedLookup[edgeEntity].m_Seed;
                    }

                    ECB.AddComponent(definitionEntity, creationDefinition);
                    ECB.AddComponent<Updated>(definitionEntity);

                    // Create NetCourse component
                    var netCourse = new NetCourse {
                        m_Curve      = adjustedBezier,
                        m_Length     = MathUtils.Length(adjustedBezier),
                        m_FixedIndex = -1,
                        m_Elevation  = default,
                        m_StartPosition = new CoursePos {
                            m_Entity        = Entity.Null,
                            m_Position      = adjustedBezier.a,
                            m_Rotation      = NetUtils.GetNodeRotation(MathUtils.StartTangent(adjustedBezier)),
                            m_CourseDelta   = 0,
                            m_Elevation     = default,
                            m_Flags         = 0,
                            m_ParentMesh    = -1,
                            m_SplitPosition = 0,
                        },
                        m_EndPosition = new CoursePos {
                            m_Entity        = Entity.Null,
                            m_Position      = adjustedBezier.d,
                            m_Rotation      = NetUtils.GetNodeRotation(MathUtils.EndTangent(adjustedBezier)),
                            m_CourseDelta   = 1,
                            m_Elevation     = default,
                            m_Flags         = 0,
                            m_ParentMesh    = -1,
                            m_SplitPosition = 0,
                        },
                    };

                    ECB.AddComponent(definitionEntity, netCourse);

                    // Progress along the path
                    cumulativeDistance += data.Length;
                }

                // Dispose temporary collections
                edgeData.Dispose();
            }
        }
    }
}