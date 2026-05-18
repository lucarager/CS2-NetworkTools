namespace NetworkTools.Systems.Tools.RoadShape {
    using NetworkTools.Systems.Tools.Parameters;

    public enum ShapeTransformTemplate {
        [EnumOption("NetworkTools.UI.Slope.Preserve", "coui://nt/Modes/Original.svg", Group = "Slope", Visible = false)]
        [EnumOption("NetworkTools.UI.Curve.Preserve", "coui://nt/Modes/Original.svg", Group = "Curve", Visible = false)]
        Preserve = 0,

        [EnumOption("NetworkTools.UI.Slope.ConstantSlope", "coui://nt/Modes/SlopeLinear.svg", Group = "Slope")]
        SlopeLinear = 1,

        [EnumOption("NetworkTools.UI.Slope.EaseInOutSlope", "coui://nt/Modes/SlopeEaseInOut.svg", Group = "Slope")]
        SlopeEaseInOut = 2,

        [EnumOption("NetworkTools.UI.Slope.Arch", "coui://nt/Modes/SlopeArch.svg", Group = "Slope", Visible = false)]
        SlopeArch = 3,

        [EnumOption("NetworkTools.UI.Curve.StraightenCurve", "coui://nt/Modes/CurveStraighten.svg", Group = "Curve")]
        CurveStraighten = 4,

        [EnumOption("NetworkTools.UI.Curve.SmoothCurve", "coui://nt/Modes/CurveSmooth.svg", Group = "Curve", Disabled = true)]
        CurveSmooth = 5,
    }
}
