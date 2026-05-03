namespace NetworkTools.Systems.Tools.Parameters {
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    ///     Discovers public <see cref="ParameterBase" /> fields on any object (typically an <see cref="NT_BaseToolSystem" /> subclass).
    ///     Per-type field lists are cached; per-instance parameter arrays are built on first access.
    /// </summary>
    public static class ParameterSchema {
        private static readonly Dictionary<Type, FieldInfo[]> s_FieldCache = new();

        internal static ParameterBase[] Discover(object instance) {
            var type = instance.GetType();
            if (!s_FieldCache.TryGetValue(type, out var fields)) {
                var discovered = new List<FieldInfo>();
                foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) {
                    if (typeof(ParameterBase).IsAssignableFrom(f.FieldType))
                        discovered.Add(f);
                }
                fields = discovered.ToArray();
                s_FieldCache[type] = fields;
            }

            var result = new ParameterBase[fields.Length];
            for (var i = 0; i < fields.Length; i++)
                result[i] = (ParameterBase)fields[i].GetValue(instance);
            return result;
        }
    }
}
