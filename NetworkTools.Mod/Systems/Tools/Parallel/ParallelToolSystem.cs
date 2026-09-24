// <copyright file="ParallelToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools.Parallel {
    using Game.Net;
    using Game.Prefabs;

    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.Parallel;
    using NetworkTools.Systems.Tools.Parameters;

    using Unity.Entities;

    /// <summary>
    ///     Tool system for creating parallel roads.
    ///     Allows selecting a contiguous path of road nodes and creating a parallel copy.
    /// </summary>
    /// <remarks>
    ///     This tool demonstrates the NT_PathSelectionToolSystem base class.
    ///     Selection, phase management, and path preview are all inherited.
    /// </remarks>
    public partial class NT_ParallelToolSystem : NT_PathSelectionToolSystem, IToolPrefabProvider, IManualApplyProvider {
        /// <inheritdoc />
        public override string toolID => "ParallelTool";

        /// <inheritdoc />
        public override bool SupportsAnarchy => true;

        /// <inheritdoc />
        public override bool TunnelAvailable {
            get {
                var prefab = NetPrefab.NetPrefabEntity;

                // Without a network picked, the copy takes the network of the path's first node.
                if (prefab == Entity.Null && NetPrefab.NetLanePrefabEntity == Entity.Null) {
                    if (m_CurrentPathEdges.Length == 0) {
                        return true;
                    }

                    var edge = EntityManager.GetComponentData<Edge>(m_CurrentPathEdges[0]);

                    prefab = EntityManager.GetComponentData<PrefabRef>(edge.m_Start).m_Prefab;
                }

                return CanTunnel(prefab);
            }
        }

        public NetPrefabParameter          NetPrefab           = new("parallel.netPrefab");
        public FloatParameter              HorizontalOffset    = new("parallel.horizontalOffset", 20f, -80f, 240f, label: "NetworkTools.UI.Parallel.HorizontalOffset", fractionDigits: 0, numberType: NumberType.Distance);
        public FloatParameter              VerticalOffset      = new("parallel.verticalOffset",   0f,  -80f, 240f, label: "NetworkTools.UI.Parallel.VerticalOffset", fractionDigits: 0, numberType: NumberType.Distance);
        public EnumParameter<ParallelDirection> ReverseDirection = new("parallel.reverseDirection", ParallelDirection.Same, label: "NetworkTools.UI.Parallel.Direction");
        public EnumParameter<ParallelOrigin>   Origin           = new("parallel.origin", ParallelOrigin.Center, label: "NetworkTools.UI.Parallel.Origin");

        /// <summary>
        ///     Tunnel mode.
        ///     Off: the copy stays at or above the line between its nodes, or at or below it.
        ///     The sign of the vertical offset decides which, and the copy follows the ground.
        ///     On: the copy is held on its curve whatever the ground does (tunnels under hills).
        ///     Each curve is cut so that a tunnel starts where the whole width is deep enough.
        ///     See the definitions job.
        /// </summary>
        public BoolParameter Tunnel = new("parallel.tunnel", false, label: "NetworkTools.UI.Common.Tunnel");

        #region Template Method Implementations

        /// <inheritdoc />
        protected override void OnPathReady() {
            m_Log.Debug("ParallelTool: Path ready - could create preview handles here");
            // In a full implementation:
            // - Create handles for adjusting parallel offset
            // - Generate preview of parallel road
        }

        /// <inheritdoc />
        protected override void OnSelectionCleared() {
            m_Log.Debug("ParallelTool: Selection cleared - cleaning up");
            DestroyAllHandles();
        }

        /// <inheritdoc />
        protected override void OnPathExtended(Entity newEndNode) {
            m_Log.Debug($"ParallelTool: Path extended to {newEndNode}");
            // In a full implementation:
            // - Update preview to include new segment
        }

        /// <inheritdoc />
        protected override void OnPathTrimmed(Entity newEndNode) {
            m_Log.Debug($"ParallelTool: Path trimmed to {newEndNode}");
            // In a full implementation:
            // - Update preview to reflect shorter path
        }

        #endregion
    }
}
