namespace NetworkTools.Systems.Tools {
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;

    using Unity.Entities;
    using Unity.Jobs;

    /// <summary>
    ///     Job scheduling and output methods for <see cref="NT_GridToolSystem"/>.
    /// </summary>
    public partial class NT_GridToolSystem {
        private JobHandle ScheduleDefinitionsJob(JobHandle inputDeps, ToolOutputMode outputMode) {
            m_Log.Debug("ScheduleDefinitionsJob");

            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);

            // If both are null, we fall back to the prefab of the first edge's start node
            //if (m_SelectedNetPrefabEntity == Entity.Null && m_SelectedNetLanePrefabEntity == Entity.Null)
            //{
            //    var firstEdge = EntityManager.GetComponentData<Edge>(m_CurrentPathEdges[0]);
            //    var prefabRef = EntityManager.GetComponentData<PrefabRef>(firstEdge.m_Start);
            //    m_SelectedNetPrefab = m_PrefabSystem.GetPrefab<NetPrefab>(prefabRef);
            //    m_SelectedNetPrefabEntity = prefabRef.m_Prefab;
            //}

            var jobHandle = new CreateDefinitionsJob {
                OutputMode = outputMode,
                Config = CurrentConfig,
                NetPrefabEntity = m_SelectedNetPrefabEntity,
                NetLanePrefabEntity = m_SelectedNetLanePrefabEntity,
                ECB = m_Barrier.CreateCommandBuffer()
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
