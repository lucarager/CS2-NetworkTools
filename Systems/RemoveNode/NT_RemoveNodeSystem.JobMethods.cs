// <copyright file="NT_NodeSelectionToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;
    using Unity.Entities;
    using Unity.Jobs;

    #endregion

    public partial class NT_RemoveNodeSystem {
        private JobHandle UpdateDefinitions(JobHandle inputDeps) {
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);

            var createDefinitionJobHandle = new CreateDefinitionJob
            {
                ControlPoint           = m_LastHoveredEntity,
                NodeLookup             = SystemAPI.GetComponentLookup<Node>(true),
                CurveLookup            = SystemAPI.GetComponentLookup<Curve>(true),
                EdgeLookup             = SystemAPI.GetComponentLookup<Edge>(true),
                PrefabRefLookup        = SystemAPI.GetComponentLookup<PrefabRef>(true),
                PseudoRandomSeedLookup = SystemAPI.GetComponentLookup<PseudoRandomSeed>(true),
                TerrainHeight          = m_TerrainSystem.GetHeightData(false),
                CurveConfig            = m_OperationState.Config,
                ECB                    = m_Barrier.CreateCommandBuffer(),
                RenderBuffer           = m_OverlayRenderSystem.GetBuffer(out var renderBufferJobHandle),
            }.Schedule(JobHandle.CombineDependencies(
                           inputDeps,
                           renderBufferJobHandle
                       ));
            m_TerrainSystem.AddCPUHeightReader(createDefinitionJobHandle);
            m_Barrier.AddJobHandleForProducer(createDefinitionJobHandle);

            return createDefinitionJobHandle;
        }

        private JobHandle Update(JobHandle inputDeps) {
            // Check if we can reuse existing temp entities
            var canReuse = false;

            if (canReuse) {
                applyMode = ApplyMode.None;
                return inputDeps;
            }

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
            applyMode = ApplyMode.Clear;
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);

            //ApplySlopeToSelectedEdges(m_OperationState.Config);

            // Clear state to completely blank
            m_OperationState = OperationState.Idle();

            return inputDeps;
        }
    }
}