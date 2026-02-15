// <copyright file="NT_NodeControlToolSystem.JobMethods.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    #endregion

    public partial class NT_NodeControlToolSystem {

        private JobHandle ScheduleJob(JobHandle inputDeps) {
            var node = m_SelectedNode.Value;

            // Select positions for markers
            var connectedEdges = EntityManager.GetBuffer<ConnectedEdge>(node);
            var positions = new NativeArray<float3>(connectedEdges.Length * 2, Allocator.TempJob);

            for (int i = 0; i < connectedEdges.Length; i++) {
                var edgeEntity = connectedEdges[i].m_Edge;
                var edge = EntityManager.GetComponentData<Edge>(edgeEntity);
                var curve = EntityManager.GetComponentData<Curve>(edgeEntity);
                var isForward = edge.m_Start == node;

                // Add bezier control points as positions
                positions[i * 2 + 0] = isForward ? curve.m_Bezier.a : curve.m_Bezier.d;
                positions[i * 2 + 1] = isForward ? curve.m_Bezier.b : curve.m_Bezier.c;
            }

            var createDefinitionJobHandle = new CreateMarkersJob {
                ECB = m_Barrier.CreateCommandBuffer(),
                Positions = positions,
                MarkerPrefab = m_NTPrefabsCreateSystem.m_MarkerPrefabEntity,
            }.Schedule(inputDeps);

            m_Barrier.AddJobHandleForProducer(createDefinitionJobHandle);

            return createDefinitionJobHandle;
            //return inputDeps;
        }

        private JobHandle Update(JobHandle inputDeps) {
            // Check if we can reuse existing temp entities
            // This will be true if the selected nodes and operation config didn't change
            //if (!m_UpdateNeeded) {
            //    applyMode = ApplyMode.None;
            //    return inputDeps;
            //}

            // Recreate temp entities
            applyMode = ApplyMode.Clear;
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);
            inputDeps = ScheduleJob(inputDeps);
            
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
            return inputDeps;
        }
    }
}