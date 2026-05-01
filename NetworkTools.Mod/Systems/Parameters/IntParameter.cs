namespace NetworkTools.Systems.Parameters {
    public class IntParameter : Parameter<int> {
        public int Min { get; }
        public int Max { get; }

        public IntParameter(string key, int @default, int min, int max, int modes = 0)
            : base(key, @default, modes) {
            Min = min;
            Max = max;
        }
    }
}
