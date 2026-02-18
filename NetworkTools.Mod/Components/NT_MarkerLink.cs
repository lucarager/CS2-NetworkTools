// <copyright file="NT_MarkerLink.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Components {
    #region Using Statements

    using Unity.Entities;

    #endregion

    public struct NT_MarkerLink : IComponentData {
        /// <summary>
        /// The primary entity this marker is associated with (e.g., a node).
        /// </summary>
        public Entity LinkedEntity;

        /// <summary>
        /// If this is a Bezier marker, the edge entity whose curve this marker controls.
        /// </summary>
        public Entity LinkedEdge;

        /// <summary>
        /// If this is a Bezier marker, the bezier control point index (0=a, 1=b, 2=c, 3=d).
        /// </summary>
        public int Key;
    }
}