namespace NetworkTools.Systems.Tools.Generate {
    using NetworkTools.Systems.Tools.Parameters;

    /// <summary>
    ///     Defines the type of transformation to apply
    /// </summary>
    public enum GenerateMode {
        None = 0,

        [EnumOption("NetworkTools.UI.Generate.Grid", "coui://nt/Modes/GenerateGrid.svg")]
        Grid = 1,

        [EnumOption("NetworkTools.UI.Generate.Circle", "coui://nt/Modes/GenerateCircle.svg")]
        Circle = 2,
    }
}
