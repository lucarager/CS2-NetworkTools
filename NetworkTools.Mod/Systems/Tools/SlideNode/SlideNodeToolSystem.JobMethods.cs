// <copyright file="SlideNodeToolSystem.JobMethods.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Colossal.Mathematics;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    #endregion

    public partial class NT_SlideNodeToolSystem {
        private JobHandle UpdateDefinitions(JobHandle inputDeps, ToolOutputMode outputMode) {
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);

            var createDefinitionJobHandle = new CreateDefinitionJob {
                NodeEntity = m_DragNodeEntity,
                Edge1Entity = m_Edge1Entity,
                Edge2Entity = m_Edge2Entity,
                HitPosition = m_LastControlPoint.m_HitPosition,
                CurvePosition = m_SnappedCurvePosition,
                ParentBezier = m_ParentBezier,
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

        /// <summary>
        /// Projects the hit position onto the parent bezier and snaps the result
        /// to stay within valid range of the neighbor nodes.
        /// </summary>
        private void SnapControlPoint(float3 hitPosition, JobHandle inputDeps) {
            if (m_DragNodeEntity == Entity.Null || m_Edge1Entity == Entity.Null || m_Edge2Entity == Entity.Null) {
                return;
            }

            // Project hit position onto parent bezier to get raw curve parameter
            MathUtils.Distance(m_ParentBezier, hitPosition, out var rawCurvePosition);

            var snappedPosition = new NativeReference<float>(rawCurvePosition, Allocator.TempJob);
            var snappedHitPosition = new NativeReference<float3>(hitPosition, Allocator.TempJob);
            var parentBezierRef = new NativeReference<Bezier4x3>(m_ParentBezier, Allocator.TempJob);

            var snapJob = new SnapControlPointJob {
                Edge1Entity = m_Edge1Entity,
                Edge2Entity = m_Edge2Entity,
                NodeEntity = m_DragNodeEntity,
                RawCurvePosition = rawCurvePosition,
                EdgeLookup = SystemAPI.GetComponentLookup<Edge>(true),
                CurveLookup = SystemAPI.GetComponentLookup<Curve>(true),
                PrefabRefLookup = SystemAPI.GetComponentLookup<PrefabRef>(true),
                NetGeometryDataLookup = SystemAPI.GetComponentLookup<NetGeometryData>(true),
                ConnectedEdgeLookup = SystemAPI.GetBufferLookup<ConnectedEdge>(true),
                SnappedCurvePosition = snappedPosition,
                SnappedHitPosition = snappedHitPosition,
                ParentBezier = parentBezierRef,
            };

            snapJob.Schedule(inputDeps).Complete();

            m_SnappedCurvePosition = snappedPosition.Value;
            m_ParentBezier = parentBezierRef.Value;

            snappedPosition.Dispose();
            snappedHitPosition.Dispose();
            parentBezierRef.Dispose();
        }

        private JobHandle Update(JobHandle inputDeps) {
            if (m_DragNodeEntity == Entity.Null) {
                return inputDeps;
            }

            if (!m_UpdateNeeded) {
                applyMode = ApplyMode.None;
                return inputDeps;
            }

            // Recreate temp entities with updated positions
            applyMode = ApplyMode.Clear;
            inputDeps = UpdateDefinitions(inputDeps, ToolOutputMode.Preview);

            m_UpdateNeeded = false;

            return inputDeps;
        }

        private JobHandle Clear(JobHandle inputDeps) {
            applyMode = ApplyMode.Clear;
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);
            return inputDeps;
        }

        private JobHandle Apply(JobHandle inputDeps) {
            if (m_DragNodeEntity == Entity.Null) {
                return inputDeps;
            }

            applyMode = ApplyMode.Apply;
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);
            inputDeps = UpdateDefinitions(inputDeps, ToolOutputMode.Apply);

            // Reset drag state
            m_IsDragging = false;
            m_DragNodeEntity = Entity.Null;
            Phase = OperationPhase.Idle;

            return inputDeps;
        }
    }
}
