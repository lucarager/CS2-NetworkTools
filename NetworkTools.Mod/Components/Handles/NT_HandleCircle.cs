// <copyright file="NT_HandleCircle.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Components.Handles {
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// Data for a circle handle representing a radius/dimension.
    /// Dragging changes the radius based on distance from center.
    /// Useful for controlling influence radius, curve radius, etc.
    /// </summary>
    public struct NT_HandleCircle : IComponentData {
        /// <summary>
        /// Center point of the circle.
        /// </summary>
        public float3 Center;

        /// <summary>
        /// Radius of the circle.
        /// </summary>
        public float Radius;

        /// <summary>
        /// Normal vector defining the plane the circle lies on.
        /// </summary>
        public float3 Normal;

        /// <summary>
        /// Creates a horizontal circle handle (normal pointing up).
        /// </summary>
        public static NT_HandleCircle CreateHorizontal(float3 center, float radius) {
            return new NT_HandleCircle {
                Center = center,
                Radius = radius,
                Normal = new float3(0, 1, 0),
            };
        }

        /// <summary>
        /// Creates a circle handle with a custom normal.
        /// </summary>
        public static NT_HandleCircle Create(float3 center, float radius, float3 normal) {
            return new NT_HandleCircle {
                Center = center,
                Radius = radius,
                Normal = math.normalizesafe(normal),
            };
        }
    }
}
