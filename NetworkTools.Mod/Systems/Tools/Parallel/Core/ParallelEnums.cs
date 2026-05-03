namespace NetworkTools.Systems.Tools.Parallel {
    /// <summary>
    ///     Determines which side of the original path the parallel copy is placed on.
    /// </summary>
    public enum ParallelSide {
        /// <summary>Offset to the left of the path direction.</summary>
        Left = 0,

        /// <summary>Offset to the right of the path direction.</summary>
        Right = 1
    }

    /// <summary>
    ///     Determines whether the vertical offset goes up or down.
    /// </summary>
    public enum VerticalSide {
        /// <summary>Offset upward.</summary>
        Up = 0,

        /// <summary>Offset downward.</summary>
        Down = 1
    }
}
