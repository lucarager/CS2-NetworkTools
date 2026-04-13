// <copyright file="NT_CEToolSystem.Lifecycle.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license
// information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using System.Collections.Generic;

    using Game.Input;
    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;

    using NetworkTools.Components.Tools;
    using NetworkTools.Settings;

    using Unity.Collections;
    using Unity.Entities;

    public partial class NT_AddNodeToolSystem {
        /// <inheritdoc />
        public bool HasToolComponent(PrefabBase prefab) { return m_PrefabSystem.HasComponent<NT_AddNodeTool>(prefab); }

        protected override void OnCreate() {
            base.OnCreate();

            m_Log.Prefix = nameof(NT_AddNodeToolSystem);

            // Configuration
            EligibilityTarget           = EligibilityTarget.Edge;
            RenderTempEdges             = true;
            RenderTempNodes             = true;
            RenderEligibleNodes         = true;
            DisableVanillaValidation    = true;
            DisableVanillaNodeReduction = true;
        }

        protected override void OnStartRunning() {
            base.OnStartRunning();

            Phase = OperationPhase.Idle;

            MarkEligibleEntities();
        }

        protected override void OnStopRunning() {
            m_Log.Debug("OnStopRunning: Cleaning up state components");

            base.OnStopRunning();
        }
    }
}