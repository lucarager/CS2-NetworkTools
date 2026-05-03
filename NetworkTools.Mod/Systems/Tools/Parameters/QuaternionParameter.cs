namespace NetworkTools.Systems.Tools.Parameters {
    using Unity.Mathematics;

    /// <summary>
    ///     Parameter holding a <c>quaternion</c> value (rotation/direction).
    ///     Not bindable by default — the Colossal binding bridge has no <c>ValueWriter&lt;quaternion&gt;</c>.
    /// </summary>
    public class QuaternionParameter : Parameter<quaternion> {
        public QuaternionParameter(string key, quaternion @default = default, int modes = 0)
            : base(key, @default, modes, bindable: false) { }
    }
}
