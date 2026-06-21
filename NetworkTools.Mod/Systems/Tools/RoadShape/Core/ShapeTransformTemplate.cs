namespace NetworkTools.Systems.Tools.RoadShape {
    using NetworkTools.Systems.Tools.Parameters;

    /// <summary>
    ///     Values are bit flags (distinct powers of two): parameters combine them into a mode mask
    ///     and the active template is matched against it bitwise. Keep new entries as powers of two.
    /// </summary>
    public enum ShapeTransformTemplate {
        [EnumOption("NetworkTools.UI.Slope.Preserve", "coui://nt/Modes/Original.svg", Group = "Slope", Visible = false)]
        [EnumOption("NetworkTools.UI.Curve.Preserve", "coui://nt/Modes/Original.svg", Group = "Curve", Visible = false)]
        Preserve = 0,

        [EnumOption("NetworkTools.UI.Slope.ConstantSlope", "coui://nt/Modes/SlopeLinear.svg", Group = "Slope")]
        SlopeLinear = 1,

        [EnumOption("NetworkTools.UI.Slope.EaseInOutSlope", "coui://nt/Modes/SlopeEaseInOut.svg", Group = "Slope")]
        SlopeEaseInOut = 2,

        [EnumOption("NetworkTools.UI.Slope.ArchSlope", "coui://nt/Modes/SlopeArch.svg", Group = "Slope")]
        SlopeArch = 4,

        [EnumOption("NetworkTools.UI.Curve.StraightenCurve", "coui://nt/Modes/CurveStraighten.svg", Group = "Curve")]
        CurveStraighten = 8,

        [EnumOption("NetworkTools.UI.Curve.SmoothCurve", "coui://nt/Modes/CurveSmooth.svg", Group = "Curve", Disabled = true)]
        CurveSmooth = 16,
    }
}
