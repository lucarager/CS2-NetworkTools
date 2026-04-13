namespace NetworkTools.Systems.Tools.Connect {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Unity.Mathematics;

    /// <summary>
    /// Constants for transform handle identification.
    /// Used in NT_HandleLink.Key to map handles to config parameters.
    /// </summary>
    public static class HandleKeys {
        // Shared Handles (0-99)
        public const int StartPosition = 0;
        public const int EndPosition = 1;
        public const int StartDirection = 2;
        public const int EndDirection = 3;

        // Curve handles (100-199)
        public const int CurveStartPointPosition = 100;
        public const int CurveStartControlPointPosition = 101;
        public const int CurveEndControlPointPosition = 102;
        public const int CurveEndPointPosition = 103;

        // Loop handles (200-299)
        public const int LoopControlPointPosition = 200;
        public const int LoopRadius = 201;
    }
}
