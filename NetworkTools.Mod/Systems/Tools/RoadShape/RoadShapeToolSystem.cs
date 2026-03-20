namespace NetworkTools.Systems.Tools.RoadShape {
    using NetworkTools.Systems.Tools;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Tool system for reshaping road segments.
    ///     Allows selecting a contiguous path of road nodes and applying transformations.
    /// </summary>
    public partial class NT_RoadShapeToolSystem : NT_PathSelectionToolSystem {
        /// <inheritdoc />
        public override string toolID => "RoadShapeTool";
        
        /// <summary>
        ///     Caches the last hit position for tool-specific use.
        /// </summary>
        private float3 m_LastHitPosition;

        /// <summary>
        ///     Tracks whether an update/re-render is needed on the next frame.
        ///     This is set to true when something changes that requires regenerating preview entities.
        ///     Gets reset to false after being processed.
        /// </summary>
        private bool m_UpdateNeeded;

        /// <summary>
        ///     Current transformation config.
        /// </summary>
        internal ShapeTransformConfig ShapeTransformConfig;

        #region Template Method Implementations

        /// <inheritdoc />
        protected override void OnPathReady() {
            RefreshPathData();
            RefreshTransformHandles();
        }

        /// <inheritdoc />
        protected override void OnSelectionCleared() {
            DestroyAllHandles();
            InvalidatePathData();
        }

        /// <inheritdoc />
        protected override void OnPathExtended(Entity newEndNode) {
            RefreshPathData();
            RefreshTransformHandles();
        }

        /// <inheritdoc />
        protected override void OnPathTrimmed(Entity newEndNode) {
            RefreshPathData();
            RefreshTransformHandles();
        }

        #endregion
    }
}
