// <copyright file="NT_CEToolSystem.Lifecycle.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    #region Using Statements

    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    #endregion

    public partial class NT_RemoveNodeToolSystem {
        public override bool TrySetPrefab(PrefabBase prefab) {
            m_Log.Debug($"TrySetPrefab {prefab is NT_ToolPrefab} {m_PrefabSystem.HasComponent<Components.NT_RemoveNode>(prefab)}");
            var validRequest = prefab is NT_ToolPrefab && m_PrefabSystem.HasComponent<Components.NT_RemoveNode>(prefab);

            if (!validRequest) {
                return false;
            }

            m_Prefab = prefab;
            return true;
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_Log.Prefix = nameof(NT_RemoveNodeToolSystem);

            // Configuration
            RenderEligibleNodes                = true;
            DisableVanillaValidation = true;
        }

        protected override void OnStartRunning() {
            base.OnStartRunning();

            Phase = OperationPhase.Idle;

            // Add NT_Eligible only to nodes with exactly 2 connected edges (non-intersection, non-endpoint)
            MarkEligibleNodes();
        }

        /// <summary>
        /// Marks nodes as eligible for removal if they have exactly 2 connected edges.
        /// These are intermediate nodes that can be removed by merging their two edges.
        /// </summary>
        private void MarkEligibleNodes() {
            var nodeQuery = SystemAPI.QueryBuilder()
                                     .WithAll<Node>()
                                     .WithNone<Components.NT_Eligible>()
                                     .Build();

            var nodeEntities = nodeQuery.ToEntityArray(Allocator.Temp);

            foreach (var nodeEntity in nodeEntities) {
                if (!EntityManager.HasBuffer<ConnectedEdge>(nodeEntity)) {
                    continue;
                }

                var connectedEdges = EntityManager.GetBuffer<ConnectedEdge>(nodeEntity);
                
                // Only nodes with exactly 2 connected edges are eligible for removal
                if (connectedEdges.Length == 2) {
                    EntityManager.AddComponent<Components.NT_Eligible>(nodeEntity);
                }
            }

            nodeEntities.Dispose();
        }

        protected override void OnStopRunning() {
            m_Log.Debug("OnStopRunning: Cleaning up state components");

            base.OnStopRunning();
        }
    }
}