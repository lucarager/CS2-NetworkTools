namespace NetworkTools.Systems.Tools.Parameters {
    public class FloatParameter : Parameter<float> {
        public float Min { get; }
        public float Max { get; }
        public int FractionDigits { get; }

        public FloatParameter(string key, float @default, float min, float max, int modes = 0, string label = null, int fractionDigits = 1)
            : base(key, @default, modes, label: label) {
            Min = min;
            Max = max;
            FractionDigits = fractionDigits;
        }
    }
}
