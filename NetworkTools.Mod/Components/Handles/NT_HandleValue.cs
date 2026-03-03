// <copyright file="NT_HandleValue.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Components.Handles {
    #region Using Statements

    using Unity.Entities;

    #endregion

    /// <summary>
    /// Scalar value data for parameter handles.
    /// Used for handles that control a numeric value (e.g., smoothing factor, radius).
    /// </summary>
    public struct NT_HandleValue : IComponentData {
        /// <summary>
        /// The current value of the parameter.
        /// </summary>
        public float Value;

        /// <summary>
        /// The minimum allowed value.
        /// </summary>
        public float MinValue;

        /// <summary>
        /// The maximum allowed value.
        /// </summary>
        public float MaxValue;

        /// <summary>
        /// Creates a new handle value with the specified range.
        /// </summary>
        public static NT_HandleValue Create(float value, float minValue, float maxValue) {
            return new NT_HandleValue {
                Value = value,
                MinValue = minValue,
                MaxValue = maxValue,
            };
        }

        /// <summary>
        /// Creates a new handle value with 0-1 range.
        /// </summary>
        public static NT_HandleValue CreateNormalized(float value) {
            return Create(value, 0f, 1f);
        }
    }
}
