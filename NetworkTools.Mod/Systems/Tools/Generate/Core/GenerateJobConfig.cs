namespace NetworkTools.Systems.Tools.Generate {
    using Unity.Mathematics;

    /// <summary>
    ///     Burst-compatible snapshot of Generate tool parameters.
    ///     Built by <c>NT_GenerateToolSystem.ScheduleDefinitionsJob</c> immediately before job scheduling.
    /// </summary>
    public struct GenerateJobConfig {
        public float3     Position;
        public quaternion StartDirection;
        public float      GridXSpacing;
        public float      GridZSpacing;
        public int        GridXNum;
        public int        GridZNum;
        public float      CircleRadius;
    }
}
