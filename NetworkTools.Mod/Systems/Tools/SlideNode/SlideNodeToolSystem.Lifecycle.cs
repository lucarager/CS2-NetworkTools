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
            UseCustomEligibilityFilter  = true;
        }

        protected override void OnStartRunning() {
            base.OnStartRunning();

            Phase = OperationPhase.Idle;
            m_IsDragging = false;
            m_DragNodeEntity = Entity.Null;

            MarkEligibleNodes();
        }

        /// <inheritdoc/>
        protected override bool FilterEligibleEntity(Entity entity) {
            if (!EntityManager.HasBuffer<ConnectedEdge>(entity)) {
                return false;
            }

            // Only nodes with exactly 2 connected edges are eligible for sliding
            return EntityManager.GetBuffer<ConnectedEdge>(entity).Length == 2;
        }

        protected override void OnStopRunning() {
            m_Log.Debug("OnStopRunning: Cleaning up state components");
            m_IsDragging = false;
            m_DragNodeEntity = Entity.Null;

            base.OnStopRunning();
        }
    }
}
