namespace NetworkTools.Systems.Tools.Parameters {
    using System;

    /// <summary>
    ///     Non-generic interface so UISystem can register enum bindings without knowing TEnum at compile time.
    /// </summary>
    public interface IEnumParameter {
        string Key      { get; }
        int    IntValue { get; set; }
    }

    /// <summary>
    ///     Enum parameter. Transported over the UI bridge as <c>int</c> via <see cref="IEnumParameter" />.
    /// </summary>
    public class EnumParameter<TEnum> : Parameter<TEnum>, IEnumParameter
        where TEnum : struct, Enum {
        public EnumParameter(string key, TEnum @default, int modes = 0, string label = null, bool persist = true)
            : base(key, @default, modes, label: label, persist: persist) { }

        public int IntValue {
            get => (int)(object)Value;
            set => Value = (TEnum)(object)value;
        }

        /// <inheritdoc />
        public override string SerializeValue() => IntValue.ToString();

        /// <inheritdoc />
        public override bool TryDeserializeValue(string raw) {
            if (!int.TryParse(raw, out var v)) return false;
            IntValue = v;
            return true;
        }
    }
}
