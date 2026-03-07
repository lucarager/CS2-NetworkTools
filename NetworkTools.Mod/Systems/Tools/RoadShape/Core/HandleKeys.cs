namespace NetworkTools.Systems.Tools.RoadShape {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Constants for transform handle identification.
    /// Used in NT_HandleLink.Key to map handles to config parameters.
    /// </summary>
    public static class HandleKeys {
        // Shape handles (100-199)
        public const int SmoothCtrl1 = 100;
        public const int SmoothCtrl2 = 101;
        public const int SmoothingFactor = 102;

        // Slope handles (200-299)
        public const int EaseInLength = 200;
        public const int EaseOutLength = 201;
        public const int ArchHeight = 210;
        public const int ArchPosition = 211;
    }
}
