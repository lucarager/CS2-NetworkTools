namespace NetworkTools.Systems.Tools.Connect {
    using NetworkTools.Systems.Tools.Parameters;

    /// <summary>
    ///     Determines whether the loop arc takes the outer (larger) or inner (smaller) path.
    /// </summary>
    public enum LoopArcSide {
        [EnumOption("NetworkTools.UI.Connect.LoopOuterArc", "coui://nt/ArcSide/Outer.svg")]
        Outer = 0,

        [EnumOption("NetworkTools.UI.Connect.LoopInnerArc", "coui://nt/ArcSide/Inner.svg")]
        Inner = 1,
    }
}
