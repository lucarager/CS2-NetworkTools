namespace NetworkTools.Systems.Tools.Parallel {
    using Colossal.Entities;

    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;

    using NetworkTools.Components;
    using NetworkTools.Settings;
    using NetworkTools.Systems.Tools.RoadShape;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    public partial class NT_ParallelToolSystem {
        private JobHandle SchedulePathTransformJob(JobHandle inputDeps, ToolOutputMode outputMode) {
            if (m_CurrentPathEdges.Length == 0) {
                return inputDeps;
            }

            if (m_SelectedNetPrefabEntity == Entity.Null) {
                // Fall back to the prefab of the first edge's start node
                var firstEdge  = EntityManager.GetComponentData<Edge>(m_CurrentPathEdges[0]);
                var prefabRef  = EntityManager.GetComponentData<PrefabRef>(firstEdge.m_Start);
                m_SelectedNetPrefab       = m_PrefabSystem.GetPrefab<NetPrefab>(prefabRef);
                m_SelectedNetPrefabEntity = prefabRef.m_Prefab;
            }

            var jobHandle = new CreateDefinitionsJob {
                OutputMode             = outputMode,
                Config                 = CurrentConfig,
                CurrentPathNodes       = m_CurrentPathNodes,
                CurrentPathEdges       = m_CurrentPathEdges,
                PrefabEntity           = m_SelectedNetPrefabEntity,
                NodeLookup             = SystemAPI.GetComponentLookup<Node>(true),
                CurveLookup            = SystemAPI.GetComponentLookup<Curve>(true),
                EdgeLookup             = SystemAPI.GetComponentLookup<Edge>(true),
                UpgradedLookup         = SystemAPI.GetComponentLookup<Upgraded>(true),
                PrefabRefLookup        = SystemAPI.GetComponentLookup<PrefabRef>(true),
                PseudoRandomSeedLookup = SystemAPI.GetComponentLookup<PseudoRandomSeed>(true),
                ConnectedEdgeLookup    = SystemAPI.GetBufferLookup<ConnectedEdge>(true),
                AggregatedLookup       = SystemAPI.GetComponentLookup<Aggregated>(true),
                ECB                    = m_Barrier.CreateCommandBuffer(),
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
            inputDeps = SchedulePathTransformJob(inputDeps, ToolOutputMode.Preview);

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
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);
            var jobHandle = SchedulePathTransformJob(inputDeps, ToolOutputMode.Apply);

            jobHandle.Complete();

            ResetToIdle();

            return jobHandle;
        }
    }
}
