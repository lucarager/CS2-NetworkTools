// <copyright file="NT_HandleLine.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Components.Handles {
    #region Using Statements

    using Unity.Entities;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// Data for a line handle representing two connected points.
    /// Line handles are useful for controlling segments or paired control points.
    /// Dragging translates both points together, maintaining their relative positions.
    /// </summary>
    public struct NT_HandleLine : IComponentData {
        /// <summary>
        /// First endpoint of the line segment.
        /// </summary>
        public float3 PointA;

        /// <summary>
        /// Second endpoint of the line segment.
        /// </summary>
        public float3 PointB;

        /// <summary>
        /// Creates a line handle with the specified endpoints.
        /// </summary>
        public static NT_HandleLine Create(float3 pointA, float3 pointB) {
            return new NT_HandleLine {
                PointA = pointA,
                PointB = pointB,
            };
        }

        /// <summary>
        /// Gets the midpoint of the line segment.
        /// </summary>
        public float3 Midpoint => (PointA + PointB) * 0.5f;

        /// <summary>
        /// Gets the length of the line segment.
        /// </summary>
        public float Length => math.distance(PointA, PointB);

        /// <summary>
        /// Gets the normalized direction from PointA to PointB.
        /// </summary>
        public float3 Direction => math.normalizesafe(PointB - PointA);
    }
}
