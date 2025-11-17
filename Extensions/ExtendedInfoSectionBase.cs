// <copyright file="${File.FileName}" company="${User.FullName}">
// Copyright (c) ${User.Name}. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworkTools.Extensions {
    using System;
    using Colossal.UI.Binding;
    using Game.UI;
    using Game.UI.InGame;

    public abstract partial class ExtendedInfoSectionBase : InfoSectionBase {
        public ValueBindingHelper<T> CreateBinding<T>(string key, T initialValue) {
            var helper = new ValueBindingHelper<T>(new(NetworkToolsMod.Id, $"BINDING:{key}", initialValue, new GenericUIWriter<T>()));

            AddBinding(helper.Binding);

            return helper;
        }

        public ValueBindingHelper<T> CreateBinding<T>(string key, T initialValue, Action<T> updateCallBack = null) {
            var helper = new ValueBindingHelper<T>(new(NetworkToolsMod.Id, $"BINDING:{key}", initialValue, new GenericUIWriter<T>()), updateCallBack);
            var trigger = new TriggerBinding<T>(NetworkToolsMod.Id, $"TRIGGER:{key}", helper.UpdateCallback, new GenericUIReader<T>());

            AddBinding(helper.Binding);
            AddBinding(trigger);

            return helper;
        }

        public ValueBindingHelper<T> CreateBinding<T>(string key, string setterKey, T initialValue, Action<T> updateCallBack = null) {
            var helper = new ValueBindingHelper<T>(new(NetworkToolsMod.Id, $"BINDING:{key}", initialValue, new GenericUIWriter<T>()), updateCallBack);
            var trigger = new TriggerBinding<T>(NetworkToolsMod.Id, $"TRIGGER:{setterKey}", helper.UpdateCallback, new GenericUIReader<T>());

            AddBinding(helper.Binding);
            AddBinding(trigger);

            return helper;
        }

        public GetterValueBinding<T> CreateBinding<T>(string key, Func<T> getterFunc) {
            var binding = new GetterValueBinding<T>(NetworkToolsMod.Id, key, getterFunc, new GenericUIWriter<T>());

            AddBinding(binding);

            return binding;
        }

        public TriggerBinding CreateTrigger(string key, Action action) {
            var binding = new TriggerBinding(NetworkToolsMod.Id, $"TRIGGER:{key}", action);

            AddBinding(binding);

            return binding;
        }

        public TriggerBinding<T1> CreateTrigger<T1>(string key, Action<T1> action) {
            var binding = new TriggerBinding<T1>(NetworkToolsMod.Id, $"TRIGGER:{key}", action, new GenericUIReader<T1>());

            AddBinding(binding);

            return binding;
        }

        public TriggerBinding<T1, T2> CreateTrigger<T1, T2>(string key, Action<T1, T2> action) {
            var binding = new TriggerBinding<T1, T2>(NetworkToolsMod.Id, $"TRIGGER:{key}", action, new GenericUIReader<T1>(), new GenericUIReader<T2>());

            AddBinding(binding);

            return binding;
        }

        public TriggerBinding<T1, T2, T3> CreateTrigger<T1, T2, T3>(string key, Action<T1, T2, T3> action) {
            var binding = new TriggerBinding<T1, T2, T3>(NetworkToolsMod.Id, $"TRIGGER:{key}", action, new GenericUIReader<T1>(), new GenericUIReader<T2>(), new GenericUIReader<T3>());

            AddBinding(binding);

            return binding;
        }

        public TriggerBinding<T1, T2, T3, T4> CreateTrigger<T1, T2, T3, T4>(string key, Action<T1, T2, T3, T4> action) {
            var binding = new TriggerBinding<T1, T2, T3, T4>(NetworkToolsMod.Id, $"TRIGGER:{key}", action, new GenericUIReader<T1>(), new GenericUIReader<T2>(), new GenericUIReader<T3>(), new GenericUIReader<T4>());

            AddBinding(binding);

            return binding;
        }
    }
}
