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

    public partial class NT_CeToolSystem {
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
                var startNode      = SelectedNodes[0];
                var endNode        = SelectedNodes[^1];
                var startNodeInfo  = NodeLookup[startNode];
                var endNodeInfo    = NodeLookup[endNode];
                var startHeight    = startNodeInfo.m_Position.y;
                var endHeight      = endNodeInfo.m_Position.y;
                var deltaHeight    = endHeight - startHeight;

                // Calculate total length of the path using the edge beziers
                var segmentLengths = new NativeList<float>(CurrentPathEdges.Length, Allocator.Temp);
                var totalLength    = 0f;

                foreach (var edgeEntity in CurrentPathEdges) {
                    if (CurveLookup.TryGetComponent(edgeEntity, out var curve)) {
                        var segmentLength = curve.m_Length;
                        segmentLengths.Add(segmentLength);
                        totalLength += segmentLength;
                    } else {
                        segmentLengths.Add(0f);
                    }
                }

                if (totalLength <= 0f) {
                    segmentLengths.Dispose();
                    return;
                }

                // Now that we have the heights, we can process each edge and adjust their curves
                var distanceAlongPath = 0f;

                for (var i = 0; i < CurrentPathEdges.Length; i++) {
                    var edgeEntity = CurrentPathEdges[i];

                    // NetCourse
                    if (!CurveLookup.TryGetComponent(edgeEntity, out var curve)) {
                        continue;
                    }

                    if (!EdgeLookup.TryGetComponent(edgeEntity, out var edge)) {
                        continue;
                    }

                    // Entity to hold definitions for this edge
                    var definitionEntity = ECB.CreateEntity();

                    // CD
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

                    // NetCourse
                    var adjustedBezier      = curve.m_Bezier;
                    var segmentLength       = segmentLengths[i];
                    var totalHorizontalDist = curve.m_Length;

                    // Calculate parametric positions of control points based on horizontal distance
                    var horizontalA = new float3(adjustedBezier.a.x, 0f, adjustedBezier.a.z);
                    var horizontalB = new float3(adjustedBezier.b.x, 0f, adjustedBezier.b.z);
                    var horizontalC = new float3(adjustedBezier.c.x, 0f, adjustedBezier.c.z);
                    var horizontalD = new float3(adjustedBezier.d.x, 0f, adjustedBezier.d.z);

                    // Calculate ratios for control points within the segment
                    var bRatio = 1f / 3f;
                    var cRatio = 2f / 3f;

                    if (totalHorizontalDist > 0.01f) {
                        bRatio = math.distance(horizontalA, horizontalB) / totalHorizontalDist;
                        cRatio = math.distance(horizontalA, horizontalC) / totalHorizontalDist;
                    }

                    bRatio = math.clamp(bRatio, 0f, 1f);
                    cRatio = math.clamp(cRatio, 0f, 1f);

                    // Calculate distances along entire path for each bezier point
                    var distA = distanceAlongPath;
                    var distB = distanceAlongPath + segmentLength * bRatio;
                    var distC = distanceAlongPath + segmentLength * cRatio;
                    var distD = distanceAlongPath + segmentLength;

                    // Calculate ratios along entire path 
                    var ratioA = distA / totalLength;
                    var ratioB = distB / totalLength;
                    var ratioC = distC / totalLength;
                    var ratioD = distD / totalLength;

                    // Apply curves
                    var curvedA = CurveConfig.ApplyCurve(ratioA);
                    var curvedB = CurveConfig.ApplyCurve(ratioB);
                    var curvedC = CurveConfig.ApplyCurve(ratioC);
                    var curvedD = CurveConfig.ApplyCurve(ratioD);

                    // Set heights using curved ratios
                    adjustedBezier.a.y = startHeight + deltaHeight * curvedA;
                    adjustedBezier.b.y = startHeight + deltaHeight * curvedB;
                    adjustedBezier.c.y = startHeight + deltaHeight * curvedC;
                    adjustedBezier.d.y = startHeight + deltaHeight * curvedD;

                    //RenderBuffer.DrawCircle(Color.white, adjustedBezier.a, 2f);
                    //RenderBuffer.DrawCircle(Color.red, adjustedBezier.b, 2f);
                    //RenderBuffer.DrawCircle(Color.blue, adjustedBezier.c, 2f);
                    //RenderBuffer.DrawCircle(Color.green, adjustedBezier.d, 2f);
                    //RenderBuffer.DrawDashedCurve(Color.black, adjustedBezier, 1f, 1f, 1f);

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
                            m_Rotation      = NetUtils.GetNodeRotation(MathUtils.StartTangent(adjustedBezier)),
                            m_CourseDelta   = 1,
                            m_Elevation     = default,
                            m_Flags         = 0,
                            m_ParentMesh    = -1,
                            m_SplitPosition = 0,
                        },
                    };

                    //// Adjust elevation
                    //var startDifferential = TerrainUtils.SampleHeight(ref TerrainHeight, netCourse.m_StartPosition.m_Position) - netCourse.m_StartPosition.m_Position.y;
                    //if (Mathf.Abs(startDifferential) > 4f) {
                    //    netCourse.m_StartPosition.m_Elevation = startDifferential;
                    //}

                    //var endDifferential = TerrainUtils.SampleHeight(ref TerrainHeight, netCourse.m_EndPosition.m_Position) - netCourse.m_EndPosition.m_Position.y;
                    //if (Mathf.Abs(endDifferential) > 4f) {
                    //    netCourse.m_EndPosition.m_Elevation = endDifferential;
                    //}

                    ECB.AddComponent(definitionEntity, netCourse);

                    // Progress along the path
                    distanceAlongPath += segmentLength;
                }

                // Dispose temporary collections
                segmentLengths.Dispose();
            }
        }
    }
}