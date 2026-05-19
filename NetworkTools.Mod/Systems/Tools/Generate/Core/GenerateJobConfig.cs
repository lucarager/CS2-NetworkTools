namespace NetworkTools.Systems.Tools.Generate {
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Burst-compatible snapshot of Generate tool parameters.
    ///     Built by <c>NT_GenerateToolSystem.ScheduleDefinitionsJob</c> immediately before job scheduling.
    /// </summary>
    public struct GenerateJobConfig {
        public float3     Position;
        public quaternion StartDirection;
        public Entity     NetPrefabEntity;
        public Entity     NetLanePrefabEntity;
        public float      NetWidth;
        public float      ElevationLimit;
        public float      GridXSpacing;
        public float      GridZSpacing;
        public int        GridXNum;
        public int        GridZNum;
        public bool       AltPrefabX;
        public Entity     AltNetPrefabXEntity;
        public Entity     AltNetLanePrefabXEntity;
        public float      AltNetPrefabXWidth;
        public int        AltEveryX;
        public bool       AltPrefabZ;
        public Entity     AltNetPrefabZEntity;
        public Entity     AltNetLanePrefabZEntity;
        public float      AltNetPrefabZWidth;
        public int        AltEveryZ;
        public float      CircleRadius;
        public float      OvalRadiusX;
        public float      OvalRadiusZ;
        public float      Elevation;
        public float      BaselineElevation;
    }
}
