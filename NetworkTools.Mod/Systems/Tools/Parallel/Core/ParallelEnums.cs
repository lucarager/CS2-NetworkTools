namespace NetworkTools.Systems.Tools.Parallel {
    using NetworkTools.Systems.Tools.Parameters;

    /// <summary>
    ///     Determines whether the parallel copy runs in the same or reverse direction.
    /// </summary>
    public enum ParallelDirection {
        [EnumOption("NetworkTools.UI.Parallel.Same", "coui://nt/Direction/Same.svg")]
        Same = 0,

        [EnumOption("NetworkTools.UI.Parallel.Reverse", "coui://nt/Direction/Opposite.svg")]
        Reverse = 1
    }

    /// <summary>
    ///     Determines the reference point from which the parallel offset is measured.
    /// </summary>
    public enum ParallelOrigin {
        [EnumOption("NetworkTools.UI.Parallel.LeftEdge", "coui://nt/Origin/LeftEdge.svg")]
        LeftEdge = 0,

        [EnumOption("NetworkTools.UI.Parallel.Center", "coui://nt/Origin/Center.svg")]
        Center = 1,

        [EnumOption("NetworkTools.UI.Parallel.RightEdge", "coui://nt/Origin/RightEdge.svg")]
        RightEdge = 2
    }
}
