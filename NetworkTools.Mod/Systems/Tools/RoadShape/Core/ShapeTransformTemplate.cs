namespace NetworkTools.Systems.Tools.RoadShape {
    /// <summary>
    ///     Defines the type of transformation to apply.
    /// </summary>
    public enum ShapeTransformTemplate {
        /// <summary>
        ///     Keep existing positions (no-op).
        /// </summary>
        Preserve = 0,

        /// <summary>
        ///     Linear slope interpolation - constant slope throughout the path.
        /// </summary>
        SlopeLinear = 1,

        /// <summary>
        ///     Ease-in-out slope - smooth transitions at start and end with steeper middle section.
        /// </summary>
        SlopeEaseInOut = 2,

        /// <summary>
        ///     Arch slope - creates an arch (hill) or dip (valley) along the path.
        /// </summary>
        SlopeArch = 3,

        /// <summary>
        ///     Straight curve - Align all nodes along a straight line between start/end.
        /// </summary>
        CurveStraighten = 4,

        /// <summary>
        ///     Smooth curve - Fit nodes to a smooth Bézier curve.
        /// </summary>
        CurveSmooth = 5,
    }
}
