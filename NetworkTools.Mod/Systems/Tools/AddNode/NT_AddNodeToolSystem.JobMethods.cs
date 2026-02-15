// <copyright file="NT_NodeSelectionToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license
// information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Entities;
    using Unity.Jobs;

    #endregion

    public partial class NT_AddNodeToolSystem {
        private JobHandle UpdateDefinitions(JobHandle inputDeps) {
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
            }.Schedule(JobHandle.CombineDependencies(
                                                     inputDeps,
                                                     renderBufferJobHandle
                                                    ));
            m_TerrainSystem.AddCPUHeightReader(createDefinitionJobHandle);
            m_Barrier.AddJobHandleForProducer(createDefinitionJobHandle);

            return createDefinitionJobHandle;
        }

        private JobHandle Update(JobHandle inputDeps, bool updateNeeded) {
            // Guard
            if (m_LastHoveredEntity.Value == Entity.Null) {
                return inputDeps;
            }

            var canReuse = !updateNeeded;

            //if (canReuse) {
            //    applyMode = ApplyMode.None;
            //    return inputDeps;
            //}

            // Recreate temp entities
            applyMode = ApplyMode.Clear;
            inputDeps = UpdateDefinitions(inputDeps);
            return inputDeps;
        }

        private JobHandle Clear(JobHandle inputDeps) {
            applyMode = ApplyMode.Clear;
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);
            return inputDeps;
        }

        private JobHandle Apply(JobHandle inputDeps) {
            // Guard
            if (m_LastHoveredEntity.Value == Entity.Null) {
                return inputDeps;
            }

            applyMode = ApplyMode.Apply;
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);
            inputDeps = UpdateDefinitions(inputDeps);

            // Clear state to completely blank
            Phase = OperationPhase.Idle;

            return inputDeps;
        }
    }
}