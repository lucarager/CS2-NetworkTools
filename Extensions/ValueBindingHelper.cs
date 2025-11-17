// <copyright file="ValueBindingHelper.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Extensions {
    using System;
    using Colossal.UI.Binding;

    public class ValueBindingHelper<T> {
        public ValueBinding<T> Binding {
            get;
        }

        public T Value {
            get => Binding.value; set => Binding.Update(value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValueBindingHelper{T}"/> class.
        /// </summary>
        /// <param name="binding"></param>
        /// <param name="updateCallBack"></param>
        public ValueBindingHelper(ValueBinding<T> binding, Action<T> updateCallBack = null) {
            Binding = binding;
            _updateCallBack = updateCallBack;
        }

        public void UpdateCallback(T value) {
            Binding.Update(value);
            _updateCallBack?.Invoke(value);
        }

        public static implicit operator T(ValueBindingHelper<T> helper) {
            return helper.Binding.value;
        }

        private readonly Action<T> _updateCallBack;
    }
}
