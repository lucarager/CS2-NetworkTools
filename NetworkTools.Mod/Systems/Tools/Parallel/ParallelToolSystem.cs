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

        public NetPrefabParameter          NetPrefab           = new("parallel.netPrefab");
        public FloatParameter              HorizontalOffset    = new("parallel.horizontalOffset", 20f, 0f, 80f);
        public FloatParameter              VerticalOffset      = new("parallel.verticalOffset",   0f,  0f, 80f);
        public EnumParameter<ParallelSide> HorizontalDirection = new("parallel.horizontalDirection", ParallelSide.Right);
        public EnumParameter<VerticalSide> VerticalDirection   = new("parallel.verticalDirection",   VerticalSide.Up);
        public BoolParameter               ReverseDirection    = new("parallel.reverseDirection", false);

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
