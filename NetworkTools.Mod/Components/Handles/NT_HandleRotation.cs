// <copyright file="NT_HandleRotation.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Components.Handles {
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// Rotation-specific data for a rotation handle.
    /// Always paired with <see cref="NT_HandleCircle"/> which provides the shared circle geometry
    /// (center, radius, normal) used for hit detection and rendering.
    /// Dragging the circle outline changes the angle rather than the radius.
    /// </summary>
    public struct NT_HandleRotation : IComponentData {
        /// <summary>
        /// Reference direction on the plane from which the angle is measured (zero-angle direction).
        /// Must be perpendicular to the circle's normal and normalized.
        /// </summary>
        public float3 ReferenceDirection;

        /// <summary>
        /// Current angle in radians, measured from <see cref="ReferenceDirection"/>
        /// around the circle's normal.
        /// </summary>
        public float Angle;

        /// <summary>
        /// Creates a rotation component with a horizontal reference (along +X).
        /// </summary>
        /// <param name="angle">Initial angle in radians.</param>
        public static NT_HandleRotation CreateHorizontal(float angle = 0f) {
            return new NT_HandleRotation {
                ReferenceDirection = new float3(1, 0, 0),
                Angle              = angle,
            };
        }

        /// <summary>
        /// Creates a rotation component with a custom reference direction.
        /// </summary>
        /// <param name="referenceDirection">Zero-angle direction on the plane (must be perpendicular to normal).</param>
        /// <param name="angle">Initial angle in radians.</param>
        public static NT_HandleRotation Create(float3 referenceDirection, float angle = 0f) {
            return new NT_HandleRotation {
                ReferenceDirection = math.normalizesafe(referenceDirection),
                Angle              = angle,
            };
        }

        /// <summary>
        /// Gets the unit direction on the plane at the current angle.
        /// Requires the circle normal to compute the perpendicular axis.
        /// This is the most commonly used output: tools can multiply by a length/radius as needed.
        /// </summary>
        /// <param name="normal">The circle's normal vector (from <see cref="NT_HandleCircle.Normal"/>).</param>
        public float3 GetDirection(float3 normal) {
            var perpendicular = math.cross(normal, ReferenceDirection);
            return ReferenceDirection * math.cos(Angle)
                 + perpendicular      * math.sin(Angle);
        }
    }
}
