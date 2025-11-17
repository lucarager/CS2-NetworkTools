// <copyright file="EnumReader.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Extensions {
    using Colossal.UI.Binding;

    public class EnumReader<T> : IReader<T> {
        /// <inheritdoc/>
        public void Read(IJsonReader reader, out T value) {
            reader.Read(out int value2);
            value = (T)(object)value2;
        }
    }
}
