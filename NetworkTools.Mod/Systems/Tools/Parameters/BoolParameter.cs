namespace NetworkTools.Systems.Tools.Parameters {
    public class BoolParameter : Parameter<bool> {
        public BoolParameter(string key, bool @default, int modes = 0, string label = null)
            : base(key, @default, modes, label: label) { }
    }
}
