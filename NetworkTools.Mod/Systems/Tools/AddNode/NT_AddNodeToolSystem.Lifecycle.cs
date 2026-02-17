// <copyright file="NT_CEToolSystem.Lifecycle.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license
// information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using NetworkTools.Components;
    using NetworkTools.Settings;
    using Unity.Collections;
    using Unity.Entities;

    public partial class NT_AddNodeToolSystem {
        public override bool TrySetPrefab(PrefabBase prefab) {
            m_Log.Debug($"TrySetPrefab {prefab is NT_ToolPrefab} {m_PrefabSystem.HasComponent<NT_AddNode>(prefab)}");
            var validRequest = prefab is NT_ToolPrefab &&
                               m_PrefabSystem.HasComponent<NT_AddNode>(prefab);

            if (!validRequest) {
                return false;
            }

            m_Prefab = prefab;
            return true;
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_Log.Prefix = nameof(NT_AddNodeToolSystem);

            // Configuration
            RenderTempEdges = true;
            RenderEligibleNodes       = true;
        }

        protected override void OnStartRunning() {
            base.OnStartRunning();

            Phase = OperationPhase.Idle;
        }

        protected override void OnStopRunning() {
            m_Log.Debug("OnStopRunning: Cleaning up state components");

            base.OnStopRunning();
        }
    }
}