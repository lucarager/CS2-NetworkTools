namespace NetworkTools.Systems.Tools.Parameters {
    public class FloatParameter : Parameter<float> {
        public float Min { get; }
        public float Max { get; }
        public int FractionDigits { get; }
        public NumberType NumberType { get; }
        public float DisplayScale { get; }

        public FloatParameter(string key, float @default, float min, float max, int modes = 0, string label = null, int fractionDigits = 1, NumberType numberType = NumberType.None, float displayScale = 1f, bool persist = true)
            : base(key, @default, modes, label: label, persist: persist) {
            Min = min;
            Max = max;
            FractionDigits = fractionDigits;
            NumberType = numberType;
            DisplayScale = displayScale;
        }
    }
}
