namespace NetworkTools.Systems.Tools.Parameters {
    public class IntParameter : Parameter<int> {
        public int Min { get; }
        public int Max { get; }

        public IntParameter(string key, int @default, int min, int max, int modes = 0, string label = null)
            : base(key, @default, modes, label: label) {
            Min = min;
            Max = max;
        }
    }
}
