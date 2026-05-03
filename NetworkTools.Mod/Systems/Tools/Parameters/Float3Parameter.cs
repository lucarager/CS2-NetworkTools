namespace NetworkTools.Systems.Tools.Parameters {
    using Unity.Mathematics;

    /// <summary>
    ///     Parameter holding a <c>float3</c> value (position, direction, etc.).
    ///     Not bindable by default — the Colossal binding bridge has no <c>ValueWriter&lt;float3&gt;</c>.
    /// </summary>
    public class Float3Parameter : Parameter<float3> {
        public Float3Parameter(string key, float3 @default = default, int modes = 0)
            : base(key, @default, modes, bindable: false) { }
    }
}
