namespace NetworkTools.Systems.Tools.Parameters {
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

        /// <summary>Locale key used as the display label in the UI (e.g. "NetworkTools.UI.Generate.XSpacing").</summary>
        public string Label { get; }

        /// <summary>Whether this parameter's value should be persisted across tool sessions.</summary>
        public bool Persist { get; }

        /// <summary>Fired whenever <see cref="Value" /> changes or <see cref="ForceNotify" /> is called.</summary>
        public event Action<ChangeOrigin> OnChanged;

        protected ParameterBase(string key, int modes = 0, bool bindable = true, string label = null, bool persist = true) {
            Key      = key;
            Modes    = modes;
            Bindable = bindable;
            Label    = label;
            Persist  = persist;
        }

        /// <summary>Reset value to the declared default. Always fires <see cref="OnChanged" />.</summary>
        public abstract void ResetToDefault();

        /// <summary>Serialize the current value to a string for persistence.</summary>
        public virtual string SerializeValue() => null;

        /// <summary>Deserialize a persisted string back into the parameter value.</summary>
        public virtual bool TryDeserializeValue(string raw) => false;

        /// <summary>Fire <see cref="OnChanged" /> unconditionally (e.g., to re-sync UI on tool activation).</summary>
        public void ForceNotify() => OnChanged?.Invoke(ChangeOrigin.Code);

        protected void RaiseChanged(ChangeOrigin origin = ChangeOrigin.Code) => OnChanged?.Invoke(origin);
    }
}
