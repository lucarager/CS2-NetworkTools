// <copyright file="EdgePositions.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// Pre-calculated XZ positions for an edge's control points in path order.
    /// </summary>
    public struct EdgePositions {
        /// <summary>
        /// XZ position at path-start of segment.
        /// </summary>
        public float2 Start;

        /// <summary>
        /// XZ position at control point closer to path-start.
        /// </summary>
        public float2 CtrlStart;

        /// <summary>
        /// XZ position at control point closer to path-end.
        /// </summary>
        public float2 CtrlEnd;

        /// <summary>
        /// XZ position at path-end of segment.
        /// </summary>
        public float2 End;
    }
}
