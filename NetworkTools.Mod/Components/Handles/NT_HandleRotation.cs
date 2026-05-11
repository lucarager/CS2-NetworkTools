// <copyright file="NT_HandleRotation.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Components.Handles {
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// Rotation handle data with its own geometry.
    /// Dragging the circle outline changes the angle rather than the radius.
    /// The circle's center position is stored in <see cref="NT_HandlePosition"/>.
    /// </summary>
    public struct NT_HandleRotation : IComponentData {
        /// <summary>
        /// Reference direction on the plane from which the angle is measured (zero-angle direction).
        /// Must be perpendicular to <see cref="Normal"/> and normalized.
        /// </summary>
        public float3 ReferenceDirection;

        /// <summary>
        /// Current angle in radians, measured from <see cref="ReferenceDirection"/>
        /// around <see cref="Normal"/>.
        /// </summary>
        public float Angle;

        /// <summary>
        /// Size of the rotation circle.
        /// </summary>
        public float Radius;

        /// <summary>
        /// Normal vector defining the plane the rotation circle lies on.
        /// </summary>
        public float3 Normal;

        /// <summary>
        /// Creates a horizontal rotation handle (normal pointing up, reference along +X).
        /// </summary>
        public static NT_HandleRotation CreateHorizontal(float radius, float angle = 0f) {
            return new NT_HandleRotation {
                ReferenceDirection = new float3(1, 0, 0),
                Angle              = angle,
                Radius             = radius,
                Normal             = new float3(0, 1, 0),
            };
        }

        /// <summary>
        /// Creates a rotation component with custom geometry.
        /// </summary>
        public static NT_HandleRotation Create(float radius, float3 normal, float3 referenceDirection, float angle = 0f) {
            return new NT_HandleRotation {
                ReferenceDirection = math.normalizesafe(referenceDirection),
                Angle              = angle,
                Radius             = radius,
                Normal             = math.normalizesafe(normal),
            };
        }

        /// <summary>
        /// Gets the unit direction on the plane at the current angle.
        /// </summary>
        public float3 GetDirection() {
            var perpendicular = math.cross(Normal, ReferenceDirection);
            return ReferenceDirection * math.cos(Angle)
                 + perpendicular      * math.sin(Angle);
        }
    }
}
