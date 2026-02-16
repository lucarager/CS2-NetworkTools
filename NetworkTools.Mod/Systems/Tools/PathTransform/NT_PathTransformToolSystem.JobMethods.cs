// <copyright file="NT_PathTransformToolSystem.JobMethods.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using NetworkTools.Systems.Tools.PathTransform;
    using Unity.Entities;
    using Unity.Jobs;

    #endregion

    public partial class NT_PathTransformToolSystem {
        private JobHandle SchedulePathTransformJob(JobHandle inputDeps, TransformOutputMode outputMode) {
            var jobHandle = new PathTransformJob {
                SelectedNodes          = m_SelectedNodes,
                CurrentPathEdges       = m_CurrentPathEdges,
                CurrentPathNodes       = m_CurrentPathNodes,
                NodeLookup             = SystemAPI.GetComponentLookup<Node>(true),
                CurveLookup            = SystemAPI.GetComponentLookup<Curve>(true),
                EdgeLookup             = SystemAPI.GetComponentLookup<Edge>(true),
                UpgradedLookup         = SystemAPI.GetComponentLookup<Upgraded>(true),
                PrefabRefLookup        = SystemAPI.GetComponentLookup<PrefabRef>(true),
                PseudoRandomSeedLookup = SystemAPI.GetComponentLookup<PseudoRandomSeed>(true),
                ConnectedEdgeLookup    = SystemAPI.GetBufferLookup<ConnectedEdge>(true),
                AggregatedLookup       = SystemAPI.GetComponentLookup<Aggregated>(true),
                Config                 = TransformConfig,
                OutputMode             = outputMode,
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
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);
            inputDeps = SchedulePathTransformJob(inputDeps, TransformOutputMode.Preview);

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
            var jobHandle = SchedulePathTransformJob(inputDeps, TransformOutputMode.Apply);

            ResetToIdle();

            return jobHandle;
        }
    }
}