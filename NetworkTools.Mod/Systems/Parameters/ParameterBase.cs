namespace NetworkTools.Systems.Parameters {
    using System;
    using Colossal.Logging;

    /// <summary>
    ///     Abstract base for all declarative tool parameters.
    ///     Holds identity metadata and the <see cref="OnChanged" /> event.
    /// </summary>
    public abstract class ParameterBase {
        private static ILog s_Log;

        protected static ILog Log =>
            s_Log ??= NetworkToolsMod.Instance?.Log;

        /// <summary>Binding key used for both the Colossal binding and TS codegen.</summary>
        public string Key   { get; }

        /// <summary>Bitflag indicating which tool modes use this parameter (0 = all modes).</summary>
        public int    Modes { get; }

        /// <summary>Whether this parameter should be registered as a UI binding.</summary>
        public bool   Bindable { get; }

        /// <summary>Fired whenever <see cref="Value" /> changes or <see cref="ForceNotify" /> is called.</summary>
        public event Action OnChanged;

        protected ParameterBase(string key, int modes = 0, bool bindable = true) {
            Key      = key;
            Modes    = modes;
            Bindable = bindable;
        }

        /// <summary>Reset value to the declared default. Always fires <see cref="OnChanged" />.</summary>
        public abstract void ResetToDefault();

        /// <summary>Fire <see cref="OnChanged" /> unconditionally (e.g., to re-sync UI on tool activation).</summary>
        public void ForceNotify() => OnChanged?.Invoke();

        protected void RaiseChanged() => OnChanged?.Invoke();
    }
}
