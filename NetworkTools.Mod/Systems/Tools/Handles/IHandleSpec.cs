namespace NetworkTools.Systems.Tools.Handles {
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Parameters;
    using Unity.Entities;
    using Unity.Mathematics;

    public delegate float3 ComputePositionDelegate<in T>(NT_BaseToolSystem tool, T value);
    public delegate T      ComputeFromPositionDelegate<out T>(NT_BaseToolSystem tool, float3 worldPos);

    public interface IHandleSpec {
        HandleTypeFlags       TypeFlags    { get; }
        string                Parent       { get; }
        NT_HandleConstraints? Constraints  { get; }
        float                 Radius       { get; }

        void SyncToEntity(NT_BaseToolSystem tool, Entity entity, ParameterBase param);
    }

    public interface IHandleSpec<T> : IHandleSpec {
        ComputePositionDelegate<T>     ComputePosition     { get; }
        ComputeFromPositionDelegate<T> ComputeFromPosition { get; }
    }
}
