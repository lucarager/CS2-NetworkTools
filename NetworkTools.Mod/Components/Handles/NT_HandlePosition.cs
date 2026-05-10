// <copyright file="NT_HandlePosition.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Components.Handles {
    using System;

    using Colossal.Serialization.Entities;

    using Unity.Entities;
    using Unity.Mathematics;

    public struct NT_HandlePosition : IComponentData,
        IEquatable<NT_HandlePosition> {
        public float3 Position;

        public bool Equals(NT_HandlePosition other) {
            return Position.Equals(other.Position);
        }

        public override int GetHashCode() {
            return Position.GetHashCode();
        }
    }
}
