namespace NetworkTools.Systems.Parameters {
    public class BoolParameter : Parameter<bool> {
        public BoolParameter(string key, bool @default, int modes = 0)
            : base(key, @default, modes) { }
    }
}
