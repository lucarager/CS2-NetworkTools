// <copyright file="GenericUIWriter.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Extensions {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Colossal.UI.Binding;
    using Unity.Entities;
    using UnityEngine;

    public class GenericUIWriter<T> : IWriter<T> {
        /// <inheritdoc/>
        public void Write(IJsonWriter writer, T value) {
            WriteGeneric(writer, value);
        }

        private static void WriteObject(IJsonWriter writer, Type type, object obj) {
            var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            writer.TypeBegin(type.FullName);

            foreach (var propertyInfo in properties) {
                writer.PropertyName(propertyInfo.Name);
                WriteGeneric(writer, propertyInfo.GetValue(obj));
            }

            foreach (var fieldInfo in fields) {
                writer.PropertyName(fieldInfo.Name);
                WriteGeneric(writer, fieldInfo.GetValue(obj));
            }

            writer.TypeEnd();
        }

        private static void WriteGeneric(IJsonWriter writer, object obj) {
            if (obj == null) {
                writer.WriteNull();
                return;
            }

            if (obj is IJsonWritable jsonWritable) {
                jsonWritable.Write(writer);
                return;
            }

            if (obj is int @int) {
                writer.Write(@int);
                return;
            }

            if (obj is bool @bool) {
                writer.Write(@bool);
                return;
            }

            if (obj is uint @uint) {
                writer.Write(@uint);
                return;
            }

            if (obj is float @float) {
                writer.Write(@float);
                return;
            }

            if (obj is double @double) {
                writer.Write(@double);
                return;
            }

            if (obj is string @string) {
                writer.Write(@string);
                return;
            }

            if (obj is Enum @enum) {
                writer.Write(Convert.ToInt32(@enum));
                return;
            }

            if (obj is Entity entity) {
                writer.Write(entity);
                return;
            }

            if (obj is Color color) {
                writer.Write(color);
                return;
            }

            if (obj is Array array) {
                WriteArray(writer, array);
                return;
            }

            if (obj is IEnumerable objects) {
                WriteEnumerable(writer, objects);
                return;
            }

            WriteObject(writer, obj.GetType(), obj);
        }

        private static void WriteArray(IJsonWriter writer, Array array) {
            writer.ArrayBegin(array.Length);

            for (var i = 0; i < array.Length; i++) {
                WriteGeneric(writer, array.GetValue(i));
            }

            writer.ArrayEnd();
        }

        private static void WriteEnumerable(IJsonWriter writer, object obj) {
            var list = new List<object>();

            foreach (var item in obj as IEnumerable) {
                list.Add(item);
            }

            writer.ArrayBegin(list.Count);

            foreach (var item in list) {
                WriteGeneric(writer, item);
            }

            writer.ArrayEnd();
        }
    }
}
