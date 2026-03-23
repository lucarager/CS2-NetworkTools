namespace NetworkTools.Systems.Tools {
    using Game.Prefabs;
    using Game.Tools;

    using Unity.Entities;
    using Unity.Jobs;

    /// <summary>
    ///     Job scheduling and output methods for <see cref="NT_GridToolSystem"/>.
    /// </summary>
    public partial class NT_GridToolSystem {
        private JobHandle ScheduleDefinitionsJob(JobHandle inputDeps, ToolOutputMode outputMode) {
            m_Log.Debug($"ScheduleDefinitionsJob: OutputMode={outputMode}");

            if (m_ControlPoints.Length != 2) {
                return inputDeps;
            }

            if (m_SelectedNetPrefabEntity == Entity.Null) {
                return inputDeps;
            }

            var jobHandle = new CreateDefinitionsJob {
                Config       = CurrentConfig,
                OutputMode   = outputMode,
                PrefabEntity = m_SelectedNetPrefabEntity,
                ECB          = m_Barrier.CreateCommandBuffer(),
            }.Schedule(inputDeps);
            m_Barrier.AddJobHandleForProducer(jobHandle);

            return jobHandle;
        }

        private JobHandle Update(JobHandle inputDeps) {
            if (!m_UpdateNeeded) {
                applyMode = ApplyMode.None;
                return inputDeps;
            }

            applyMode = ApplyMode.Clear;
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);
            inputDeps = ScheduleDefinitionsJob(inputDeps, ToolOutputMode.Preview);

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
