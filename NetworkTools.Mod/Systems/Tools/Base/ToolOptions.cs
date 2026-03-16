// <copyright file="ToolOptions.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using System;

    /// <summary>
    ///     Snap options available to tool systems.
    /// </summary>
    [Flags]
    public enum SnapOption {
        None     = 0,
        ZoneGrid = 1 << 0,
        MidPoint = 1 << 1,
        All      = ZoneGrid | MidPoint
    }

    /// <summary>
    ///     Target options available to tool systems.
    /// </summary>
    [Flags]
    public enum TargetOption {
        None        = 0,
        Road        = 1 << 0,
        Path        = 1 << 1,
        Rail        = 1 << 2,
        Waterway    = 1 << 3,
        All         = Road | Path | Rail | Waterway
    }
}
