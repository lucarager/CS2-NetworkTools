namespace NetworkTools.Systems.Tools.RoadShape {
    using Colossal.Entities;

    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;

    using NetworkTools.Components;
    using NetworkTools.Components;
    using NetworkTools.Settings;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    public partial class NT_RoadShapeToolSystem {
        private JobHandle SchedulePathTransformJob(JobHandle inputDeps, ToolOutputMode outputMode) {
            var jobHandle = new ShapeTransformJob {
                SelectedNodes = m_SelectedNodes,
                CurrentPathEdges = m_CurrentPathEdges,
                CurrentPathNodes = m_CurrentPathNodes,
                NodeLookup = SystemAPI.GetComponentLookup<Node>(true),
                CurveLookup = SystemAPI.GetComponentLookup<Curve>(true),
                EdgeLookup = SystemAPI.GetComponentLookup<Edge>(true),
                UpgradedLookup = SystemAPI.GetComponentLookup<Upgraded>(true),
                PrefabRefLookup = SystemAPI.GetComponentLookup<PrefabRef>(true),
                PseudoRandomSeedLookup = SystemAPI.GetComponentLookup<PseudoRandomSeed>(true),
                ConnectedEdgeLookup = SystemAPI.GetBufferLookup<ConnectedEdge>(true),
                AggregatedLookup = SystemAPI.GetComponentLookup<Aggregated>(true),
                Config = ShapeTransformConfig,
                OutputMode = outputMode,
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
            applyMode = ApplyMode.Clear;
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);
            var jobHandle = SchedulePathTransformJob(inputDeps, ToolOutputMode.Apply);

            ResetToIdle();

            return jobHandle;
        }
    }
}
