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
        // Curve handles (100-199)
        public const int CurveStartPointPosition = 100;
        public const int CurveStartControlPointPosition = 101;
        public const int CurveEndControlPointPosition = 102;
        public const int CurveEndPointPosition = 103;
    }
}
