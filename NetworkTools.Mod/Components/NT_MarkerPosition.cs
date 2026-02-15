// <copyright file="NT_MarkerPosition.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Components {
    #region Using Statements

    using System;
    using Colossal.Serialization.Entities;
    using Unity.Entities;
    using Unity.Mathematics;

    #endregion

    public struct NT_MarkerPosition : IComponentData,
        IEquatable<NT_MarkerPosition>,
        ISerializable {
        public float3 Position;
        public quaternion Rotation;

        public bool Equals(NT_MarkerPosition other) {
            return Position.Equals(other.Position) && Rotation.Equals(other.Rotation);
        }

        public override int GetHashCode() {
            return (17 * 31 + Position.GetHashCode()) * 31 + Rotation.GetHashCode();
        }

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter {
            writer.Write(Position);
            writer.Write(Rotation);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader {
            reader.Read(out Position);
            reader.Read(out Rotation);
            if (math.all(Position >= -100000f) && math.all(Position <= 100000f) &&
                math.all(math.isfinite(Rotation.value)) && !math.all(Rotation.value == 0.0f)) {
                return;
            }

            Position = new float3();
            Rotation = quaternion.identity;
        }
    }
}