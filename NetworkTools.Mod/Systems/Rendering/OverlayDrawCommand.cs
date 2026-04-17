namespace NetworkTools.Systems.Rendering {
    using Colossal.Mathematics;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    ///     Discriminator for overlay draw command types.
    /// </summary>
    internal enum OverlayCommandType : byte {
        /// <summary>A straight line segment.</summary>
        Line,

        /// <summary>A circle at a given position.</summary>
        Circle,

        /// <summary>A bezier curve.</summary>
        Curve,
    }

    /// <summary>
    ///     A Burst-compatible draw command emitted by a parallel prepare job
    ///     and consumed by the sequential render job.
    /// </summary>
    internal struct OverlayDrawCommand {
        public OverlayCommandType m_Type;
        public Color                 m_Color;
        public Bezier4x3             m_Bezier;    // Curve: bezier data  | Line/Circle: unused
        public float3                m_PointA;    // Line: start         | Circle: position
        public float3                m_PointB;    // Line: end           | Circle: unused
        public float                 m_Width;     // Line/Curve: width   | Circle: diameter
        public bool                  m_ForceUp;   // Curve: force upward
    }
}
