namespace NetworkTools.Systems.Tools {
    using System;

    /// <summary>
    ///     Snap options available to tool systems.
    /// </summary>
    [Flags]
    public enum SnapOption {
        None             = 0,
        ZoneGrid         = 1 << 0,
        MidPoint         = 1 << 1,
        ExistingGeometry = 1 << 2,
        ObjectSide       = 1 << 3,
        GuideLines       = 1 << 4,
        AllUsual = ZoneGrid | ExistingGeometry | ObjectSide | GuideLines,
        All = ZoneGrid | MidPoint | ExistingGeometry | ObjectSide | GuideLines
    }

    /// <summary>
    ///     Target options available to tool systems.
    /// </summary>
    [Flags]
    public enum TargetOption {
        None          = 0,
        Road          = 1 << 0,
        Path          = 1 << 1,
        Rail          = 1 << 2,
        Waterway      = 1 << 3,
        InvisiblePath = 1 << 4,
        Default       = Road | Path | Rail | Waterway,
        All           = Road | Path | Rail | Waterway | InvisiblePath
    }

    /// <summary>
    ///     Determines which network entity type a tool marks as eligible.
    /// </summary>
    public enum EligibilityTarget {
        /// <summary>Mark nodes as eligible (default for most tools).</summary>
        Node,

        /// <summary>Mark edges as eligible (e.g. AddNode).</summary>
        Edge
    }

    /// <summary>
    ///     View options controlling additional rendering / visualization.
    /// </summary>
    [Flags]
    public enum ViewOption {
        None              = 0,
        Underground       = 1 << 0,
        ZoneGrid          = 1 << 1,
        InvisibleNetworks = 1 << 2,
        All               = Underground | ZoneGrid | InvisibleNetworks
    }
}