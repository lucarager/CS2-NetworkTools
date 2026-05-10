namespace NetworkTools.Systems.Tools.Parameters {
    using System.Collections.Generic;
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

        protected Parameter(string key, T @default, int modes = 0, bool bindable = true) : base(key, modes, bindable) {
            Default = @default;
            m_Value = @default;
        }

        public override void ResetToDefault() {
            Log?.Debug($"[Parameter] {Key}: reset to default ({Default})");
            Value = Default;
        }
    }
}
