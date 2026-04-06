// <copyright file="RenderModes.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Components {
    using System;

    /// <summary>
    /// Render mode flags for highlighted/selected nodes.
    /// </summary>
    [Flags]
    public enum NodeRenderMode : byte {
        None           = 0,
        RenderAsCircle = 1 << 0, // Renderer should draw a circle on this node
        RenderSelected = 1 << 2, // Renderer should draw this node when selected
    }

    /// <summary>
    /// Render mode flags for highlighted/selected edges.
    /// </summary>
    [Flags]
    public enum EdgeRenderMode : byte {
        None           = 0,
        RenderOutlines = 1 << 0, // Renderer should draw outlines
        RenderCurve    = 1 << 1, // Renderer should draw the middle curve bezier
    }
}
