// <copyright file="ParallelToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools.Parallel {
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

        public NetPrefabParameter          NetPrefab           = new("parallel.netPrefab");
        public FloatParameter              HorizontalOffset    = new("parallel.horizontalOffset", 20f, -80f, 80f, label: "NetworkTools.UI.Parallel.HorizontalOffset", fractionDigits: 0, numberType: NumberType.Distance);
        public FloatParameter              VerticalOffset      = new("parallel.verticalOffset",   0f,  -80f, 80f, label: "NetworkTools.UI.Parallel.VerticalOffset", fractionDigits: 0, numberType: NumberType.Distance);
        public EnumParameter<ParallelDirection> ReverseDirection = new("parallel.reverseDirection", ParallelDirection.Same, label: "NetworkTools.UI.Parallel.Direction");
        public EnumParameter<ParallelOrigin>   Origin           = new("parallel.origin", ParallelOrigin.Center, label: "NetworkTools.UI.Parallel.Origin");

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
