namespace NetworkTools.Systems.Tools.Connect {
    using Colossal.Entities;

    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using Unity.Entities;
    using Unity.Jobs;

    public partial class NT_ConnectToolSystem {
        private JobHandle ScheduleDefinitionsJob(JobHandle inputDeps, ToolOutputMode outputMode) {
            m_Log.Debug($"ScheduleDefinitionsJob: Mode={CurrentMode}");

            if (m_SelectedNodes.Length != 2) {
                return inputDeps;
            }

            if (m_SelectedNetPrefabEntity == Entity.Null) {
                var prefabRef = EntityManager.GetComponentData<PrefabRef>(m_SelectedNodes[0]);
                m_SelectedNetPrefab = m_PrefabSystem.GetPrefab<NetPrefab>(prefabRef);
                m_SelectedNetPrefabEntity = prefabRef.m_Prefab;
            }

            var jobHandle = new CreateDefinitionsJob {
                Mode = CurrentMode,
                Config = CurrentConfig,
                SelectedNodeEntities = m_SelectedNodes,
                PrefabEntity = m_SelectedNetPrefabEntity,
                OutputMode = outputMode,

                // Lookups needed for output and intersection adjustments
                NodeLookup = SystemAPI.GetComponentLookup<Node>(true),
                CurveLookup = SystemAPI.GetComponentLookup<Curve>(true),
                EdgeLookup = SystemAPI.GetComponentLookup<Edge>(true),
                UpgradedLookup = SystemAPI.GetComponentLookup<Upgraded>(true),
                PrefabRefLookup = SystemAPI.GetComponentLookup<PrefabRef>(true),
                PseudoRandomSeedLookup = SystemAPI.GetComponentLookup<PseudoRandomSeed>(true),
                ConnectedEdgeLookup = SystemAPI.GetBufferLookup<ConnectedEdge>(true),
                AggregatedLookup = SystemAPI.GetComponentLookup<Aggregated>(true),
                ECB = m_Barrier.CreateCommandBuffer(),
            }.Schedule(inputDeps);
            m_Barrier.AddJobHandleForProducer(jobHandle);

            return jobHandle;
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
            inputDeps = ScheduleDefinitionsJob(inputDeps, ToolOutputMode.Preview);

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
            applyMode = ApplyMode.Clear;
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);
            var jobHandle = ScheduleDefinitionsJob(inputDeps, ToolOutputMode.Apply);

            ResetToIdle();

            return jobHandle;
        }
    }
}
