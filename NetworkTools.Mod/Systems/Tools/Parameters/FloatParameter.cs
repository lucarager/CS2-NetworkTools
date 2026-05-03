namespace NetworkTools.Systems.Tools.Parameters {
    public class FloatParameter : Parameter<float> {
        public float Min { get; }
        public float Max { get; }

        public FloatParameter(string key, float @default, float min, float max, int modes = 0)
            : base(key, @default, modes) {
            Min = min;
            Max = max;
        }
    }
}
