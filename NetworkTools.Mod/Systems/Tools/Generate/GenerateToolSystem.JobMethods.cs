namespace NetworkTools.Systems.Tools.Generate {
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;

    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    ///     Job scheduling and output methods for <see cref="NT_GenerateToolSystem"/>.
    /// </summary>
    public partial class NT_GenerateToolSystem {
        /// <summary>
        ///     Builds a Burst-compatible snapshot struct from the current parameter values.
        /// </summary>
        internal GenerateJobConfig BuildJobConfig() {
            return new GenerateJobConfig {
                Position       = Position.Value,
                StartDirection = quaternion.LookRotationSafe(Rotation.Value, math.up()),
                GridXSpacing   = GridXSpacing.Value,
                GridZSpacing   = GridZSpacing.Value,
                GridXNum       = GridXNum.Value,
                GridZNum       = GridZNum.Value,
                CircleRadius   = CircleRadius.Value,
                Elevation      = Elevation.Value,
            };
        }

        private JobHandle ScheduleDefinitionsJob(JobHandle inputDeps, ToolOutputMode outputMode) {
            m_Log.Debug($"ScheduleDefinitionsJob: Mode={Mode.Value}");

            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);

            var isHoverPreview = m_SelectedControlPoint.value.Equals(default);
            var controlPoint = isHoverPreview ? m_HoveredControlPoint.value : m_SelectedControlPoint.value;

            if (controlPoint.Equals(default))
            {
                m_Log.Debug("No valid control point for definition generation. Skipping job scheduling.");
                return inputDeps;
            }

            var jobHandle = new CreateDefinitionsJob {
                Mode                   = Mode.Value,
                Config                 = BuildJobConfig(),
                NetPrefabEntity        = NetPrefab.NetPrefabEntity,
                NetLanePrefabEntity    = NetPrefab.NetLanePrefabEntity,
                OutputMode             = outputMode,

                NodeLookup             = SystemAPI.GetComponentLookup<Node>(true),
                CurveLookup            = SystemAPI.GetComponentLookup<Curve>(true),
                EdgeLookup             = SystemAPI.GetComponentLookup<Edge>(true),
                UpgradedLookup         = SystemAPI.GetComponentLookup<Upgraded>(true),
                PrefabRefLookup        = SystemAPI.GetComponentLookup<PrefabRef>(true),
                PseudoRandomSeedLookup = SystemAPI.GetComponentLookup<PseudoRandomSeed>(true),
                ConnectedEdgeLookup    = SystemAPI.GetBufferLookup<ConnectedEdge>(true),
                AggregatedLookup       = SystemAPI.GetComponentLookup<Aggregated>(true),
                NetGeometryDataLookup  = SystemAPI.GetComponentLookup<NetGeometryData>(true),
                ECB                    = m_Barrier.CreateCommandBuffer(),
            }.Schedule(inputDeps);
            m_Barrier.AddJobHandleForProducer(jobHandle);

            return jobHandle;
        }

        private JobHandle Update(JobHandle inputDeps) {
            if (!m_UpdateNeeded)
            {
                applyMode = ApplyMode.None;
                return inputDeps;
            }

            applyMode = ApplyMode.Clear;
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
            applyMode = ApplyMode.Apply;
            var jobHandle = ScheduleDefinitionsJob(inputDeps, ToolOutputMode.Apply);

            jobHandle.Complete();

            ResetToIdle();

            return jobHandle;
        }
    }
}
