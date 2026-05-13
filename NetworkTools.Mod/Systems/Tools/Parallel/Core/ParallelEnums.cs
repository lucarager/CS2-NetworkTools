namespace NetworkTools.Systems.Tools.Parallel {
    using NetworkTools.Systems.Tools.Parameters;

    /// <summary>
    ///     Determines which side of the original path the parallel copy is placed on.
    /// </summary>
    public enum ParallelSide {
        [EnumOption("NetworkTools.UI.Parallel.Left", "coui://nt/Side/Left.svg")]
        Left = 0,

        [EnumOption("NetworkTools.UI.Parallel.Right", "coui://nt/Side/Right.svg")]
        Right = 1
    }

    /// <summary>
    ///     Determines whether the vertical offset goes up or down.
    /// </summary>
    public enum VerticalSide {
        [EnumOption("NetworkTools.UI.Parallel.Up", "coui://nt/Side/Up.svg")]
        Up = 0,

        [EnumOption("NetworkTools.UI.Parallel.Down", "coui://nt/Side/Down.svg")]
        Down = 1
    }

    /// <summary>
    ///     Determines whether the parallel copy runs in the same or reverse direction.
    /// </summary>
    public enum ParallelDirection {
        [EnumOption("NetworkTools.UI.Parallel.Same", "coui://nt/Direction/Same.svg")]
        Same = 0,

        [EnumOption("NetworkTools.UI.Parallel.Reverse", "coui://nt/Direction/Opposite.svg")]
        Reverse = 1
    }
}
