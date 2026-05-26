// <copyright file="AddNodeToolSystem.JobMethods.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license
// information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    public partial class NT_AddNodeToolSystem {
        private JobHandle UpdateDefinitions(JobHandle inputDeps, ToolOutputMode outputMode) {
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);

            var createDefinitionJobHandle = new CreateDefinitionJob {
                EdgeEntity = m_LastControlPoint.m_OriginalEntity,
                HitPosition = m_LastControlPoint.m_HitPosition,
                CurvePosition = m_LastControlPoint.m_CurvePosition,
                NodeLookup = SystemAPI.GetComponentLookup<Node>(true),
                CurveLookup = SystemAPI.GetComponentLookup<Curve>(true),
                EdgeLookup = SystemAPI.GetComponentLookup<Edge>(true),
                PrefabRefLookup = SystemAPI.GetComponentLookup<PrefabRef>(true),
                PseudoRandomSeedLookup = SystemAPI.GetComponentLookup<PseudoRandomSeed>(true),
                ConnectedEdgeLookup = SystemAPI.GetBufferLookup<ConnectedEdge>(true),
                TerrainHeight = m_TerrainSystem.GetHeightData(false),
                ECB = m_Barrier.CreateCommandBuffer(),
                RenderBuffer = m_OverlayRenderSystem.GetBuffer(out var renderBufferJobHandle),
                OutputMode = outputMode,
            }.Schedule(JobHandle.CombineDependencies(
                                                     inputDeps,
                                                     renderBufferJobHandle
                                                    ));
            m_TerrainSystem.AddCPUHeightReader(createDefinitionJobHandle);
            m_Barrier.AddJobHandleForProducer(createDefinitionJobHandle);

            return createDefinitionJobHandle;
        }

        private ControlPoint SnapControlPoint(ControlPoint controlPoint, JobHandle inputDeps) {
            if (controlPoint.m_OriginalEntity == Entity.Null)
            {
                return controlPoint;
            }

            var snappedPosition = new NativeReference<float>(controlPoint.m_CurvePosition, Allocator.TempJob);
            var snappedHitPosition = new NativeReference<float3>(controlPoint.m_HitPosition, Allocator.TempJob);

            var snapJob = new SnapControlPointJob {
                EdgeEntity            = controlPoint.m_OriginalEntity,
                RawCurvePosition      = controlPoint.m_CurvePosition,
                EdgeLookup            = SystemAPI.GetComponentLookup<Edge>(true),
                CurveLookup           = SystemAPI.GetComponentLookup<Curve>(true),
                PrefabRefLookup       = SystemAPI.GetComponentLookup<PrefabRef>(true),
                NetGeometryDataLookup = SystemAPI.GetComponentLookup<NetGeometryData>(true),
                ConnectedEdgeLookup   = SystemAPI.GetBufferLookup<ConnectedEdge>(true),
                SnapMode              = SnapMode.Endpoints,
                GridSize              = 8f,
                SnappedCurvePosition  = snappedPosition,
                SnappedHitPosition    = snappedHitPosition,
            };

            snapJob.Schedule(inputDeps).Complete(); // Sync completion

            controlPoint.m_CurvePosition = snappedPosition.Value;
            controlPoint.m_HitPosition   = snappedHitPosition.Value;

            snappedPosition.Dispose();
            snappedHitPosition.Dispose();

            return controlPoint;
        }
        private JobHandle Update(JobHandle inputDeps) {
            // Guard
            if (m_LastControlPoint.m_OriginalEntity == Entity.Null) {
                return inputDeps;
            }


            if (!m_UpdateNeeded) {
                applyMode = ApplyMode.None;
                return inputDeps;
            }

            // Recreate temp entities
            applyMode = ApplyMode.Clear;
            inputDeps = UpdateDefinitions(inputDeps, ToolOutputMode.Preview);
            
            // Reset the flag after processing
            m_UpdateNeeded = false;

            return inputDeps;
        }

        private JobHandle Clear(JobHandle inputDeps) {
            applyMode = ApplyMode.Clear;
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);
            return inputDeps;
        }

        private JobHandle Apply(JobHandle inputDeps) {
            // Guard
            if (m_LastControlPoint.m_OriginalEntity == Entity.Null) {
                return inputDeps;
            }


            applyMode = ApplyMode.Apply;
            inputDeps = UpdateDefinitions(inputDeps, ToolOutputMode.Apply);

            inputDeps.Complete();

            // Clear state to completely blank
            Phase = OperationPhase.Idle;
            MarkEligibleEntities();

            return inputDeps;
        }
    }
}
