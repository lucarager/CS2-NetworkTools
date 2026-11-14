namespace NetworkTools.Systems.Tools.Connect {
    using Game.Simulation;
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
        public bool  Tunnel;

        // Computed at job start
        public Entity NetPrefabEntity;
        public Entity NetLanePrefabEntity;
        public float NetWidth;
        public float ElevationLimit;

        /// <summary>
        ///     Terrain heights for this frame, so a created node can be classified against the ground
        ///     under it (bridge / cutting / on the ground) instead of against the other node.
        ///     <para>
        ///         Only valid inside the job: <see cref="HasTerrain" /> is false for the copies built on
        ///         the main thread, and the generators fall back to the node pair in that case.
        ///     </para>
        /// </summary>
        public TerrainHeightData Terrain;
        public bool              HasTerrain;

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
        public float3 ComplexMidStartControlPointPosition;
        public float3 ComplexMidEndControlPointPosition;

        // Loop
        public float     LoopRadiusFactor;

        /// <summary>
        ///     Which of the four loop shapes to build: orthogonal (90° fillet) / crossing (270°
        ///     trumpet) on intersecting roads, u-turn (180° half circle) / stacked (two 270° loops on
        ///     an elevated deck) on parallel ones.
        /// </summary>
        public LoopShape Shape;

        /// <summary>
        ///     Design grade of the whole connection, as a fraction (0.04 = 4%). A rise of
        ///     <c>Δh</c> is spent over <c>|Δh| / grade</c> metres of path; the rest stays level.
        /// </summary>
        public float LoopRampGrade;

        /// <summary>
        ///     Vertical offset of the arc, in metres; negative puts it below the road (tunnel).
        ///     <para>
        ///         It is measured from the <b>arc</b>, never from the two nodes: on
        ///         <see cref="LoopShape.Stacked" /> the deck height is "where the first loop starts"
        ///         plus this value, so the ramps rise exactly this much regardless of what the two
        ///         nodes are doing. Measuring from <c>max(startY, endY)</c> instead made the value mean
        ///         something different on every node pair, and left the ramp grade with no fixed
        ///         target to hit.
        ///     </para>
        ///     <para>
        ///         On the single-arc shapes the whole arc is shifted by this value; the straight
        ///         lead-ins adapt to keep both nodes pinned. A side that has no lead-in (its arc
        ///         endpoint <i>is</i> the node) cannot move, so the offset is dropped on that side.
        ///     </para>
        /// </summary>
        public float LoopHeight;
    }
}
