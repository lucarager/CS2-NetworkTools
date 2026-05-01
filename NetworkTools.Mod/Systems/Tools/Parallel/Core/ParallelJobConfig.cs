namespace NetworkTools.Systems.Tools.Parallel {
    /// <summary>
    ///     Burst-compatible snapshot of <see cref="ParallelTool" /> parameters.
    ///     Built by <c>ParallelToolSystem.ScheduleDefinitionsJob</c> immediately before job scheduling.
    /// </summary>
    public struct ParallelJobConfig {
        public float        HorizontalOffset;
        public float        VerticalOffset;
        public ParallelSide HorizontalDirection;
        public VerticalSide VerticalDirection;
        public bool         ReverseDirection;

        public readonly float SignedHorizontalOffset =>
            HorizontalDirection == ParallelSide.Right ? HorizontalOffset : -HorizontalOffset;

        public readonly float SignedVerticalOffset =>
            VerticalDirection == VerticalSide.Up ? VerticalOffset : -VerticalOffset;
    }
}
