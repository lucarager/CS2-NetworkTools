namespace NetworkTools.Systems.Parameters {
    using System;

    /// <summary>
    ///     Abstract base for all declarative tool parameters.
    ///     Holds identity metadata and the <see cref="OnChanged" /> event.
    /// </summary>
    public abstract class ParameterBase {
        /// <summary>Binding key used for both the Colossal binding and TS codegen.</summary>
        public string Key   { get; }

        /// <summary>Bitflag indicating which tool modes use this parameter (0 = all modes).</summary>
        public int    Modes { get; }

        /// <summary>Fired whenever <see cref="Value" /> changes or <see cref="ForceNotify" /> is called.</summary>
        public event Action OnChanged;

        protected ParameterBase(string key, int modes = 0) {
            Key   = key;
            Modes = modes;
        }

        /// <summary>Reset value to the declared default. Always fires <see cref="OnChanged" />.</summary>
        public abstract void ResetToDefault();

        /// <summary>Fire <see cref="OnChanged" /> unconditionally (e.g., to re-sync UI on tool activation).</summary>
        public void ForceNotify() => OnChanged?.Invoke();

        protected void RaiseChanged() => OnChanged?.Invoke();
    }
}
