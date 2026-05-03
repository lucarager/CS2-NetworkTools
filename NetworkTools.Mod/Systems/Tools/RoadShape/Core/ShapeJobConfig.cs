namespace NetworkTools.Systems.Tools.RoadShape {
    /// <summary>
    ///     Burst-compatible snapshot of RoadShape tool parameters.
    ///     Built by <c>NT_RoadShapeToolSystem.BuildJobConfig</c> immediately before job scheduling.
    /// </summary>
    public struct ShapeJobConfig {
        public ShapeTransformTemplate Template;
        public float EaseInLength;
        public float EaseOutLength;
        public float ArchHeight;
        public float ArchPosition;
        public float SmoothingFactor;
    }
}
