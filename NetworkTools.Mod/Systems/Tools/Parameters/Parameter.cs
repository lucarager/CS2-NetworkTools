namespace NetworkTools.Systems.Tools.Parameters {
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using NetworkTools.Systems.Tools.Handles;

    /// <summary>
    ///     Typed parameter. Fires <see cref="ParameterBase.OnChanged" /> on value change (equality-guarded).
    /// </summary>
    public abstract class Parameter<T> : ParameterBase {
        private T m_Value;

        public T Default { get; }

        public IHandleSpec<T>[] Handles { get; init; }

        public T Value {
            get => m_Value;
            set => SetValue(value, ChangeOrigin.Code);
        }

        public void SetValue(T value, ChangeOrigin origin) {
            if (EqualityComparer<T>.Default.Equals(m_Value, value)) return;
            var old = m_Value;
            m_Value = value;
            Log?.Debug($"[Parameter] {Key}: {old} → {value}");
            RaiseChanged(origin);
        }

        protected Parameter(string key, T @default, int modes = 0, bool bindable = true, string label = null, bool persist = true)
            : base(key, modes, bindable, label, persist) {
            Default = @default;
            m_Value = @default;
        }

        /// <inheritdoc />
        public override string SerializeValue() =>
            Convert.ToString(Value, CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public override bool TryDeserializeValue(string raw) {
            try {
                Value = (T)Convert.ChangeType(raw, typeof(T), CultureInfo.InvariantCulture);
                return true;
            } catch {
                return false;
            }
        }

        public override void ResetToDefault() {
            Log?.Debug($"[Parameter] {Key}: reset to default ({Default})");
            Value = Default;
        }
    }
}
