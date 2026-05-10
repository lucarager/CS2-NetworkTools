// <copyright file="NT_Handle.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Components.Handles {
    using System;

    using Unity.Entities;

    /// <summary>
    /// Flags defining the type and purpose of a handle.
    /// Handles are defined by combining multiple flags.
    /// </summary>
    [Flags]
    public enum HandleTypeFlags : uint {
        /// <summary>No flags set.</summary>
        None = 0,

        // === Bezier Handle Types ===

        /// <summary>Represents a bezier curve point.</summary>
        BezierPoint = 1 << 0,

        /// <summary>Start point of a bezier curve (a).</summary>
        BezierStartPoint = 1 << 1,

        /// <summary>End point of a bezier curve (d).</summary>
        BezierEndPoint = 1 << 2,

        /// <summary>Control point of a bezier curve (b or c).</summary>
        BezierControlPoint = 1 << 3,

        // === Transform Handle Types ===

        /// <summary>Controls world position.</summary>
        Position = 1 << 4,

        /// <summary>Controls a scalar parameter value.</summary>
        Parameter = 1 << 5,

        /// <summary>Controls rotation (future use).</summary>
        Rotation = 1 << 6,

        /// <summary>Controls scale (future use).</summary>
        Scale = 1 << 7,

        // === Purpose Categories ===

        /// <summary>Horizontal curve manipulation.</summary>
        ShapeControl = 1 << 8,

        /// <summary>Vertical elevation manipulation.</summary>
        SlopeControl = 1 << 9,

        // === Visual Hints ===

        /// <summary>Larger/prominent display.</summary>
        Primary = 1 << 10,

        /// <summary>Smaller/subdued display.</summary>
        Secondary = 1 << 11,

        // === Geometric Handle Types ===

        /// <summary>Two-point line segment.</summary>
        Line = 1 << 12,

        /// <summary>Circle/arc for radius control.</summary>
        Circle = 1 << 13,

        // === Rendering Hints ===

        /// <summary>Render with range indicator (dot at origin, line from origin to handle).</summary>
        ParameterRange = 1 << 14,
    }

    /// <summary>
    /// Tag component identifying an entity as a handle.
    /// Combined with other NT_Handle* components to define handle behavior.
    /// </summary>
    public struct NT_Handle : IComponentData {
        /// <summary>
        /// Size for primary (prominent) handles.
        /// </summary>
        public const float SizePrimary = 2f;

        /// <summary>
        /// Size for secondary (subdued) handles.
        /// </summary>
        public const float SizeSecondary = 1f;

        /// <summary>
        /// Type flags defining the handle's purpose and behavior.
        /// </summary>
        public HandleTypeFlags TypeFlags;

        /// <summary>
        /// Size used for both hit detection and visual rendering (diameter = Size * 2).
        /// </summary>
        public float Size;

        /// <summary>
        /// Checks if the handle has all of the specified flags.
        /// </summary>
        public bool HasAllFlags(HandleTypeFlags flags) => (TypeFlags & flags) == flags;

        /// <summary>
        /// Checks if the handle has any of the specified flags.
        /// </summary>
        public bool HasAnyFlag(HandleTypeFlags flags) => (TypeFlags & flags) != HandleTypeFlags.None;

        /// <summary>
        /// Creates a handle with the specified type flags and size.
        /// </summary>
        public static NT_Handle Create(HandleTypeFlags typeFlags, float size = SizePrimary) {
            return new NT_Handle { TypeFlags = typeFlags, Size = size };
        }
    }
}
