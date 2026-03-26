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
        public bool HasToolComponent(PrefabBase prefab) { return m_PrefabSystem.HasComponent<NT_ParallelTool>(prefab); }

        /// <inheritdoc />
        public bool? TryCacheNetPrefab(PrefabBase prefab) {
            switch (prefab) {
                case RoadPrefab or TrackPrefab or WaterwayPrefab or PathwayPrefab:
                {
                    m_SelectedNetPrefab           = (NetPrefab)prefab;
                    m_SelectedNetPrefabEntity     = m_PrefabSystem.GetEntity(m_SelectedNetPrefab);
                    m_SelectedNetLanePrefab       = null;
                    m_SelectedNetLanePrefabEntity = Entity.Null;
                    return m_ToolSystem.activeTool == this;
                }
                case NetLanePrefab netLanePrefab:
                {
                    m_SelectedNetLanePrefab       = netLanePrefab;
                    m_SelectedNetLanePrefabEntity = m_PrefabSystem.GetEntity(netLanePrefab);
                    m_SelectedNetPrefab           = null;
                    m_SelectedNetPrefabEntity     = Entity.Null;
                    return m_ToolSystem.activeTool == this;
                }
                default:
                    return null;
            }
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
            ClearSelectionState(false);
        }
    }
}
