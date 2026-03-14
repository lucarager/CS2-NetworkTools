// <copyright file="SlideNodeToolSystem.Lifecycle.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using Game.Net;
    using Game.Prefabs;

    using NetworkTools.Components;
    using NetworkTools.Components.Tools;

    using Unity.Collections;
    using Unity.Entities;

    public partial class NT_SlideNodeToolSystem {
        public override bool TrySetPrefab(PrefabBase prefab) {
            m_Log.Debug($"TrySetPrefab {prefab is NT_ToolPrefab} {m_PrefabSystem.HasComponent<NT_SlideNode>(prefab)}");
            var validRequest = prefab is NT_ToolPrefab &&
                               m_PrefabSystem.HasComponent<NT_SlideNode>(prefab);

            if (!validRequest) {
                return false;
            }

            m_Prefab = prefab;
            return true;
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_Log.Prefix = nameof(NT_SlideNodeToolSystem);

            // Configuration
            RenderTempEdges             = true;
            RenderTempNodes             = true;
            RenderEligibleNodes         = true;
            DisableVanillaValidation    = true;
            DisableVanillaNodeReduction = true;
        }

        protected override void OnStartRunning() {
            base.OnStartRunning();

            Phase = OperationPhase.Idle;
            m_IsDragging = false;
            m_DragNodeEntity = Entity.Null;

            MarkEligibleNodes();
        }

        /// <summary>
        /// Marks nodes as eligible for sliding if they have exactly 2 connected edges.
        /// These are intermediate nodes whose position can be slid along the parent curve.
        /// </summary>
        private void MarkEligibleNodes() {
            var nodeQuery = SystemAPI.QueryBuilder()
                                     .WithAll<Node>()
                                     .WithNone<NT_Eligible>()
                                     .Build();

            var nodeEntities = nodeQuery.ToEntityArray(Allocator.Temp);

            foreach (var nodeEntity in nodeEntities) {
                if (!EntityManager.HasBuffer<ConnectedEdge>(nodeEntity)) {
                    continue;
                }

                var connectedEdges = EntityManager.GetBuffer<ConnectedEdge>(nodeEntity);

                // Only nodes with exactly 2 connected edges are eligible for sliding
                if (connectedEdges.Length == 2) {
                    EntityManager.AddComponent<Components.NT_Eligible>(nodeEntity);
                }
            }

            nodeEntities.Dispose();
        }

        protected override void OnStopRunning() {
            m_Log.Debug("OnStopRunning: Cleaning up state components");
            m_IsDragging = false;
            m_DragNodeEntity = Entity.Null;

            base.OnStopRunning();
        }
    }
}
