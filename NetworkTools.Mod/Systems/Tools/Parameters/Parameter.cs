namespace NetworkTools.Systems.Tools.Parameters {
    using System.Collections.Generic;

    /// <summary>
    ///     Typed parameter. Fires <see cref="ParameterBase.OnChanged" /> on value change (equality-guarded).
    /// </summary>
    public abstract class Parameter<T> : ParameterBase {
        private T m_Value;

        public T Default { get; }

        public T Value {
            get => m_Value;
            set {
                if (EqualityComparer<T>.Default.Equals(m_Value, value)) return;
                var old = m_Value;
                m_Value = value;
                Log?.Debug($"[Parameter] {Key}: {old} → {value}");
                RaiseChanged();
            }
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
