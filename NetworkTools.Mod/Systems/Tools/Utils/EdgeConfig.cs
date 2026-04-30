namespace NetworkTools.Systems.Tools.Utils {
    using Colossal.Mathematics;
    using Game.Tools;
    using Unity.Entities;
    using Unity.Mathematics;

    public struct EdgeConfig {
        public Entity         EdgeEntity;
        public Entity         StartNodeEntity;
        public Entity         EndNodeEntity;
        public float3         StartPosition;
        public float3         EndPosition;
        public float2         Elevation;
        public CoursePosFlags StartNodeFlags;
        public CoursePosFlags EndNodeFlags;
        public bool           IsForward;
        public Bezier4x3      Bezier;
        public float          Length;
    }
}