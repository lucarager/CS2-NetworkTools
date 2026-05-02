namespace NetworkTools.Systems.Tools.Connect {
    using Colossal.Entities;

    using Game;
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
        /// <summary>
        ///     Builds a Burst-compatible snapshot struct from the current parameter values and contextual state.
        /// </summary>
        internal ConnectJobConfig BuildJobConfig() {
            return new ConnectJobConfig {
                StartPosition                  = StartPosition.Value,
                EndPosition                    = EndPosition.Value,
                StartDirection                 = StartDirection.Value,
                EndDirection                   = EndDirection.Value,
                CurveStartPointPosition        = CurveStartPointPosition.Value,
                CurveStartControlPointPosition = CurveStartControlPointPosition.Value,
                CurveEndControlPointPosition   = CurveEndControlPointPosition.Value,
                CurveEndPointPosition          = CurveEndPointPosition.Value,
                LoopControlPointPosition       = LoopControlPointPosition.Value,
                LoopRadius                     = LoopRadius.Value,
            };
        }

        private JobHandle ScheduleDefinitionsJob(JobHandle inputDeps, ToolOutputMode outputMode) {
            m_Log.Debug($"ScheduleDefinitionsJob: Mode={Mode.Value}");

            if (m_SelectedNodes.Length != 2) {
                return inputDeps;
            }

            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);

            // If both are null, we fall back to the prefab of the first edge's start node
            if (m_SelectedNetPrefabEntity == Entity.Null && m_SelectedNetLanePrefabEntity == Entity.Null)
            {
                var prefabRef = EntityManager.GetComponentData<PrefabRef>(m_SelectedNodes[0]);
                m_SelectedNetPrefab = m_PrefabSystem.GetPrefab<NetPrefab>(prefabRef);
                m_SelectedNetPrefabEntity = prefabRef.m_Prefab;
            }

            var jobHandle = new CreateDefinitionsJob {
                Mode = Mode.Value,
                Config = BuildJobConfig(),
                SelectedNodeEntities = m_SelectedNodes,
                PrefabEntity = m_SelectedNetPrefabEntity,
                NetLanePrefabEntity = m_SelectedNetLanePrefabEntity,
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
