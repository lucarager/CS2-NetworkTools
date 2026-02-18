// <copyright file="EdgeControlPointHeights.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    /// <summary>
    /// Pre-calculated heights for an edge's control points in path order.
    /// </summary>
    public struct EdgeControlPointHeights {
        /// <summary>
        /// Height at path-start of segment.
        /// </summary>
        public float Start;

        /// <summary>
        /// Height at control point closer to path-start.
        /// </summary>
        public float CtrlStart;

        /// <summary>
        /// Height at control point closer to path-end.
        /// </summary>
        public float CtrlEnd;

        /// <summary>
        /// Height at path-end of segment.
        /// </summary>
        public float End;
    }
}
