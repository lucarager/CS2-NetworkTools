// <copyright file="NT_HandleConstraints.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Components {
    #region Using Statements

    using System;
    using Unity.Entities;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// Flags defining movement constraints for handles.
    /// </summary>
    [Flags]
    public enum ConstraintFlags {
        /// <summary>No constraints applied.</summary>
        None = 0,

        /// <summary>Lock movement on the X axis.</summary>
        LockX = 1 << 0,

        /// <summary>Lock movement on the Y axis (XZ plane movement only).</summary>
        LockY = 1 << 1,

        /// <summary>Lock movement on the Z axis.</summary>
        LockZ = 1 << 2,

        /// <summary>Clamp position to min/max bounds.</summary>
        ClampToBounds = 1 << 3,

        /// <summary>Snap movement to a specified axis direction.</summary>
        SnapToAxis = 1 << 4,
    }

    /// <summary>
    /// Optional movement constraints for handles.
    /// Add this component to restrict how a handle can be moved.
    /// </summary>
    public struct NT_HandleConstraints : IComponentData {
        /// <summary>
        /// Active constraint flags.
        /// </summary>
        public ConstraintFlags Flags;

        /// <summary>
        /// Axis direction for SnapToAxis constraint.
        /// Should be normalized.
        /// </summary>
        public float3 SnapAxis;

        /// <summary>
        /// Minimum bounds for ClampToBounds constraint.
        /// </summary>
        public float3 MinBounds;

        /// <summary>
        /// Maximum bounds for ClampToBounds constraint.
        /// </summary>
        public float3 MaxBounds;

        /// <summary>
        /// Origin point for axis-relative constraints.
        /// </summary>
        public float3 Origin;

        /// <summary>
        /// Checks if a specific constraint flag is set.
        /// </summary>
        public bool HasFlag(ConstraintFlags flag) => (Flags & flag) != 0;

        /// <summary>
        /// Creates constraints that lock movement to the XZ plane (default for most handles).
        /// </summary>
        public static NT_HandleConstraints LockYAxis() {
            return new NT_HandleConstraints {
                Flags = ConstraintFlags.LockY,
            };
        }

        /// <summary>
        /// Creates constraints that only allow movement along a specific axis.
        /// </summary>
        /// <param name="axis">The axis direction (will be normalized).</param>
        /// <param name="origin">The origin point for the axis.</param>
        public static NT_HandleConstraints AxisOnly(float3 axis, float3 origin = default) {
            return new NT_HandleConstraints {
                Flags = ConstraintFlags.SnapToAxis,
                SnapAxis = math.normalizesafe(axis),
                Origin = origin,
            };
        }

        /// <summary>
        /// Creates constraints that clamp position within bounds.
        /// </summary>
        public static NT_HandleConstraints WithBounds(float3 minBounds, float3 maxBounds) {
            return new NT_HandleConstraints {
                Flags = ConstraintFlags.ClampToBounds,
                MinBounds = minBounds,
                MaxBounds = maxBounds,
            };
        }

        /// <summary>
        /// Creates vertical-only movement constraints (for elevation handles).
        /// </summary>
        /// <param name="origin">The XZ position to lock to.</param>
        public static NT_HandleConstraints VerticalOnly(float3 origin) {
            return new NT_HandleConstraints {
                Flags = ConstraintFlags.SnapToAxis,
                SnapAxis = new float3(0, 1, 0),
                Origin = origin,
            };
        }
    }
}
