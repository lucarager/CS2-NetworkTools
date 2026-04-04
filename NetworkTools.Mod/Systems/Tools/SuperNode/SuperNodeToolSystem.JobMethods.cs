namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    #endregion

    public partial class NT_SuperNodeToolSystem {
        private JobHandle UpdateDefinitions(JobHandle inputDeps, ToolOutputMode outputMode) {
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);

            // Build a hash set from the selected nodes for O(1) membership checks
            var selectedNodeCount = m_SelectedNodes.Length;
            var selectedNodeSet   = new NativeHashSet<Entity>(selectedNodeCount, Allocator.TempJob);
            for (var i = 0; i < selectedNodeCount; i++) {
                selectedNodeSet.Add(m_SelectedNodes[i]);
            }

            // Estimate total edge count for the processed-edge dedup set
            var processedEdges = new NativeHashSet<Entity>(selectedNodeCount * 4, Allocator.TempJob);

            var createDefinitionJobHandle = new CreateDefinitionJob {
                HoveredNode            = m_LastHoveredEntity,
                NodeLookup             = SystemAPI.GetComponentLookup<Node>(true),
                CurveLookup            = SystemAPI.GetComponentLookup<Curve>(true),
                EdgeLookup             = SystemAPI.GetComponentLookup<Edge>(true),
                UpgradedLookup         = SystemAPI.GetComponentLookup<Upgraded>(true),
                TempLookup             = SystemAPI.GetComponentLookup<Temp>(true),
                PrefabRefLookup        = SystemAPI.GetComponentLookup<PrefabRef>(true),
                PseudoRandomSeedLookup = SystemAPI.GetComponentLookup<PseudoRandomSeed>(true),
                ConnectedEdgeLookup    = SystemAPI.GetBufferLookup<ConnectedEdge>(true),
                TerrainHeight          = m_TerrainSystem.GetHeightData(),
                SelectedNodeEntities   = m_SelectedNodes,
                SelectedNodeSet        = selectedNodeSet,
                ProcessedEdges         = processedEdges,
                ECB                    = m_Barrier.CreateCommandBuffer(),
                DebugMode              = DebugMode,
                RenderBuffer           = m_OverlayRenderSystem.GetBuffer(out var renderBufferJobHandle),
                OutputMode             = outputMode
            }.Schedule(JobHandle.CombineDependencies(inputDeps,
                                                     renderBufferJobHandle));

            // Dispose temp allocations after the job completes
            selectedNodeSet.Dispose(createDefinitionJobHandle);
            processedEdges.Dispose(createDefinitionJobHandle);

            m_TerrainSystem.AddCPUHeightReader(createDefinitionJobHandle);
            m_Barrier.AddJobHandleForProducer(createDefinitionJobHandle);

            return createDefinitionJobHandle;
        }

        private JobHandle Update(JobHandle inputDeps) {
            // Check if we can reuse existing temp entities
            // This will be true if the selected nodes and operation config didn't change
            if (!m_UpdateNeeded && !DebugMode) {
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
            applyMode = ApplyMode.Apply;
            inputDeps = UpdateDefinitions(inputDeps, ToolOutputMode.Apply);

            inputDeps.Complete();

            ResetToIdle();

            return inputDeps;
        }
    }
}