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
    using NetworkTools.Components;
    using NetworkTools.Components.Tools;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    #endregion

    public partial class NT_SuperNodeToolSystem {
        public override bool TrySetPrefab(PrefabBase prefab) {
            m_Log.Debug($"TrySetPrefab {prefab is NT_ToolPrefab} {m_PrefabSystem.HasComponent<NT_SuperNode>(prefab)}");
            var validRequest = prefab is NT_ToolPrefab && m_PrefabSystem.HasComponent<NT_SuperNode>(prefab);

            if (!validRequest) {
                return false;
            }

            m_Prefab = prefab;
            return true;
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_Log.Prefix = nameof(NT_SuperNodeToolSystem);

            // Configuration
            RenderEligibleNodes        = true;
            DisableVanillaValidation   = true;
            UseCustomEligibilityFilter = true;

            // Data structures
            m_SelectedNodes = new NativeList<Entity>(32, Allocator.Persistent);
        }

        protected override void OnDestroy() {
            if (m_SelectedNodes.IsCreated) {
                m_SelectedNodes.Dispose();
            }
            base.OnDestroy();
        }

        protected override void OnStartRunning() {
            base.OnStartRunning();

            Phase = OperationPhase.Idle;

            // Ensure clean state
            m_SelectedNodes.Clear();

            MarkEligibleEntities();
        }

        /// <inheritdoc/>
        protected override bool FilterEligibleEntity(Entity entity) {
            // todo
            return true;
        }

        protected override void OnStopRunning() {
            m_Log.Debug("OnStopRunning: Cleaning up state components");

            // Clear state
            EntityManager.RemoveComponent<NT_Selected>(m_AllNtComponentsQuery);
            m_SelectedNodes.Clear();

            base.OnStopRunning();
        }
    }
}