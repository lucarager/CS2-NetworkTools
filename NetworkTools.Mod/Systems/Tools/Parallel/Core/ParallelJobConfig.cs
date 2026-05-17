namespace NetworkTools.Systems.Tools.Parallel {
    /// <summary>
    ///     Burst-compatible snapshot of <see cref="ParallelTool" /> parameters.
    ///     Built by <c>ParallelToolSystem.ScheduleDefinitionsJob</c> immediately before job scheduling.
    /// </summary>
    public struct ParallelJobConfig {
        public float             HorizontalOffset;
        public float             VerticalOffset;
        public ParallelDirection ReverseDirection;
        public ParallelOrigin    Origin;
    }
}
