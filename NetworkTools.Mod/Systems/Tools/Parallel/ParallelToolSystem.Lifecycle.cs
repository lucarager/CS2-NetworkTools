// <copyright file="ParallelToolSystem.Lifecycle.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools.Parallel {
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;

    using NetworkTools.Components;
    using NetworkTools.Components.Tools;

    using Unity.Entities;

    /// <summary>
    ///     Lifecycle methods for the Parallel tool.
    /// </summary>
    public partial class NT_ParallelToolSystem {
        /// <inheritdoc />
        public override bool TrySetPrefab(PrefabBase prefab) {
            m_Log.Debug($"TrySetPrefab {prefab is NT_ToolPrefab} {m_PrefabSystem.HasComponent<NT_Parallel>(prefab)}");
            var validRequest = prefab is NT_ToolPrefab &&
                               m_PrefabSystem.HasComponent<NT_Parallel>(prefab);

            if (!validRequest)
            {
                return false;
            }

            m_Prefab = prefab;
            return true;
        }

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            m_Log.Prefix = nameof(NT_ParallelToolSystem);

            // Configuration
            RenderEligibleNodes = true;
            RenderHandles = true;
            DisableVanillaValidation = true;

            // Initialize selection state (base class NativeLists)
            InitializeSelectionState();

            // Override default query to only select Road nodes
            m_NodesWithoutEligibleQuery = SystemAPI.QueryBuilder()
                .WithAll<Node, Road>()
                .WithNone<NT_Eligible>()
                .Build();
        }

        /// <inheritdoc />
        protected override void OnDestroy() {
            // Dispose selection state (base class NativeLists)
            DisposeSelectionState();

            base.OnDestroy();
        }

        /// <inheritdoc />
        protected override void OnStartRunning() {
            base.OnStartRunning();

            // Reset internal state
            Phase = OperationPhase.Idle;

            // Initialize selection state (makes all nodes eligible)
            ResetToNoSelection();
        }

        /// <inheritdoc />
        protected override void OnStopRunning() {
            base.OnStopRunning();

            // Clear selection state
            ClearSelectionState();
        }
    }
}
