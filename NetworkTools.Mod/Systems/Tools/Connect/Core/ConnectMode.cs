namespace NetworkTools.Systems.Tools.Connect {
    using NetworkTools.Systems.Tools.Parameters;

    /// <summary>
    ///     Defines the type of transformation to apply
    /// </summary>
    public enum ConnectMode {
        [EnumOption("NetworkTools.UI.Connect.None", "coui://nt/Modes/Original.svg", Visible = false)]
        None = 0,

        [EnumOption("NetworkTools.UI.Connect.SimpleCurve", "coui://nt/Modes/ConnectSimpleCurve.svg")]
        SimpleCurve = 1,

        [EnumOption("NetworkTools.UI.Connect.ComplexCurve", "coui://nt/Modes/ConnectComplexCurve.svg", Visible = false)]
        ComplexCurve = 2,

        [EnumOption("NetworkTools.UI.Connect.Loop", "coui://nt/Modes/ConnectLoop.svg")]
        Loop = 3,
    }
}
