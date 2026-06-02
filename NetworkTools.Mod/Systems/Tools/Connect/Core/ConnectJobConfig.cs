namespace NetworkTools.Systems.Tools.Connect {
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Burst-compatible snapshot of Connect tool state.
    ///     Built by <c>NT_ConnectToolSystem.BuildJobConfig</c> immediately before job scheduling.
    /// </summary>
    public struct ConnectJobConfig {
        // Shared
        public float3 StartPosition;
        public float3 StartDirection;
        public float  StartElevation;
        public float3 EndPosition;
        public float3 EndDirection;
        public float  EndElevation;
        public float Elevation;

        // Computed at job start
        public Entity NetPrefabEntity;
        public Entity NetLanePrefabEntity;
        public float NetWidth;
        public float ElevationLimit;

        // Simple Curve
        public float3 CurveStartPointPosition;
        public float3 CurveStartControlPointPosition;
        public float3 CurveEndControlPointPosition;
        public float3 CurveEndPointPosition;

        // Complex Curve
        public float3 ComplexStartPointPosition;
        public float3 ComplexStartControlPointPosition;
        public float3 ComplexEndControlPointPosition;
        public float3 ComplexEndPointPosition;
        public float3 ComplexMidPosition;
        public float3 ComplexMidRotation;

        // Loop
        public float LoopRadiusFactor;
        public LoopArcSide LoopArcSide;
    }
}
