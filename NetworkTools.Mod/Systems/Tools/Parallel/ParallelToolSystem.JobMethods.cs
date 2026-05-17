namespace NetworkTools.Systems.Tools.Parallel {
    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Entities;
    using Unity.Jobs;

    public partial class NT_ParallelToolSystem {
        private JobHandle ScheduleDefinitionsJob(JobHandle inputDeps, ToolOutputMode outputMode) {
            m_Log.Debug("ScheduleDefinitionsJob");

            if (m_CurrentPathEdges.Length == 0) {
                return inputDeps;
            }

            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);

            var netPrefabEntity = NetPrefab.NetPrefabEntity;
            var netLanePrefabEntity = NetPrefab.NetLanePrefabEntity;

            if (netPrefabEntity == Entity.Null && netLanePrefabEntity == Entity.Null) {
                var firstEdge = EntityManager.GetComponentData<Edge>(m_CurrentPathEdges[0]);
                var prefabRef = EntityManager.GetComponentData<PrefabRef>(firstEdge.m_Start);
                netPrefabEntity = prefabRef.m_Prefab;
            }

            var jobConfig = new ParallelJobConfig {
                HorizontalOffset = HorizontalOffset.Value,
                VerticalOffset   = VerticalOffset.Value,
                ReverseDirection = ReverseDirection.Value,
                Origin           = Origin.Value,
            };

            var jobHandle = new CreateDefinitionsJob {
                OutputMode             = outputMode,
                Config                 = jobConfig,
                CurrentPathNodes       = m_CurrentPathNodes,
                CurrentPathEdges       = m_CurrentPathEdges,
                NetPrefabEntity        = netPrefabEntity,
                NetLanePrefabEntity    = netLanePrefabEntity,
                NodeLookup             = SystemAPI.GetComponentLookup<Node>(true),
                CurveLookup            = SystemAPI.GetComponentLookup<Curve>(true),
                EdgeLookup             = SystemAPI.GetComponentLookup<Edge>(true),
                UpgradedLookup         = SystemAPI.GetComponentLookup<Upgraded>(true),
                PrefabRefLookup        = SystemAPI.GetComponentLookup<PrefabRef>(true),
                PseudoRandomSeedLookup = SystemAPI.GetComponentLookup<PseudoRandomSeed>(true),
                ConnectedEdgeLookup    = SystemAPI.GetBufferLookup<ConnectedEdge>(true),
                AggregatedLookup       = SystemAPI.GetComponentLookup<Aggregated>(true),
                NetGeometryDataLookup  = SystemAPI.GetComponentLookup<NetGeometryData>(true),
                ECB                    = m_Barrier.CreateCommandBuffer()
            }.Schedule(inputDeps);
            m_Barrier.AddJobHandleForProducer(jobHandle);

            return jobHandle;
        }

        private JobHandle Update(JobHandle inputDeps) {
            // Check if we can reuse existing temp entities
            // This will be true if the selected nodes and operation config didn't change
            if (!m_UpdateNeeded) {
                applyMode = ApplyMode.None;
                return inputDeps;
            }

            // Recreate temp entities
            applyMode = ApplyMode.Clear;
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
            applyMode = ApplyMode.Apply;
            var jobHandle = ScheduleDefinitionsJob(inputDeps, ToolOutputMode.Apply);

            jobHandle.Complete();

            ResetToIdle();

            return jobHandle;
        }
    }
}