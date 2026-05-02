namespace NetworkTools.Systems.Tools.Connect {
    using Unity.Mathematics;

    /// <summary>
    ///     Burst-compatible snapshot of Connect tool state.
    ///     Built by <c>NT_ConnectToolSystem.BuildJobConfig</c> immediately before job scheduling.
    /// </summary>
    public struct ConnectJobConfig {
        // Shared
        public float3 StartPosition;
        public float3 EndPosition;
        public float3 StartDirection;
        public float3 EndDirection;

        // Curve
        public float3 CurveStartPointPosition;
        public float3 CurveStartControlPointPosition;
        public float3 CurveEndControlPointPosition;
        public float3 CurveEndPointPosition;

        // Loop
        public float3 LoopControlPointPosition;
        public float  LoopRadius;
    }
}
