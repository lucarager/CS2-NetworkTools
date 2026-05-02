namespace NetworkTools.Systems.Tools.RoadShape {
    using NetworkTools.Systems.Parameters;
    using NetworkTools.Systems.Tools;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Tool system for reshaping road segments.
    ///     Allows selecting a contiguous path of road nodes and applying transformations.
    /// </summary>
    public partial class NT_RoadShapeToolSystem : NT_PathSelectionToolSystem, IManualApplyProvider {
        /// <inheritdoc />
        public override string toolID => "RoadShapeTool";

        // ── Parameters (declarative, reflection-driven) ──────────────────────────

        public EnumParameter<ShapeTransformTemplate> Template        = new("roadShape.template", ShapeTransformTemplate.Preserve);
        public FloatParameter                        EaseInLength    = new("roadShape.easeInLength",    0.1f, 0f, 0.4f, modes: (int)ShapeTransformTemplate.SlopeEaseInOut);
        public FloatParameter                        EaseOutLength   = new("roadShape.easeOutLength",   0.1f, 0f, 0.4f, modes: (int)ShapeTransformTemplate.SlopeEaseInOut);
        public FloatParameter                        ArchHeight      = new("roadShape.archHeight",      0.5f, -1f, 1f,  modes: (int)ShapeTransformTemplate.SlopeArch);
        public FloatParameter                        ArchPosition    = new("roadShape.archPosition",    0.5f, 0.1f, 0.9f, modes: (int)ShapeTransformTemplate.SlopeArch);
        public FloatParameter                        SmoothingFactor = new("roadShape.smoothingFactor", 0.5f, 0f, 1f,   modes: (int)ShapeTransformTemplate.CurveSmooth);

        // ── Non-parameter state ──────────────────────────────────────────────────

        /// <summary>
        ///     Caches the last hit position for tool-specific use.
        /// </summary>
        private float3 m_LastHitPosition;

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

        /// <summary>
        ///     Builds a Burst-compatible snapshot from the current parameter values.
        /// </summary>
        internal ShapeJobConfig BuildJobConfig() {
            return new ShapeJobConfig {
                Template        = Template.Value,
                EaseInLength    = EaseInLength.Value,
                EaseInMax       = EaseInLength.Max,
                EaseOutLength   = EaseOutLength.Value,
                EaseOutMax      = EaseOutLength.Max,
                ArchHeight      = ArchHeight.Value,
                ArchPosition    = ArchPosition.Value,
                SmoothingFactor = SmoothingFactor.Value,
            };
        }
    }
}
