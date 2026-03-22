// <copyright file="NT_NodeSelectionToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
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

    public partial class NT_SuperNodeToolSystem {
        private JobHandle UpdateDefinitions(JobHandle inputDeps, ToolOutputMode outputMode) {
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);

            var createDefinitionJobHandle = new CreateDefinitionJob
            {
                HoveredNode            = m_LastHoveredEntity,
                NodeLookup             = SystemAPI.GetComponentLookup<Node>(true),
                CurveLookup            = SystemAPI.GetComponentLookup<Curve>(true),
                EdgeLookup             = SystemAPI.GetComponentLookup<Edge>(true),
                TempLookup             = SystemAPI.GetComponentLookup<Temp>(true),
                PrefabRefLookup        = SystemAPI.GetComponentLookup<PrefabRef>(true),
                PseudoRandomSeedLookup = SystemAPI.GetComponentLookup<PseudoRandomSeed>(true),
                ConnectedEdgeLookup    = SystemAPI.GetBufferLookup<ConnectedEdge>(true),
                TerrainHeight          = m_TerrainSystem.GetHeightData(false),
                SelectedNodeEntities = m_SelectedNodes,
                ECB = m_Barrier.CreateCommandBuffer(),
                RenderBuffer           = m_OverlayRenderSystem.GetBuffer(out var renderBufferJobHandle),
                OutputMode = outputMode,
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
            // This will be true if the selected nodes and operation config didn't change
            if (!m_UpdateNeeded)
            {
                applyMode = ApplyMode.None;
                return inputDeps;
            }

            // Recreate temp entities
            applyMode = ApplyMode.Clear;
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);
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
            if (m_LastHoveredEntity.Value == Entity.Null) {
                return inputDeps;
            }

            applyMode = ApplyMode.Apply;
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);
            inputDeps = UpdateDefinitions(inputDeps, ToolOutputMode.Apply);

            // Clear state to completely blank
            Phase = OperationPhase.Idle;

            return inputDeps;
        }
    }
}