namespace NetworkTools.Systems.Handles {
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.Parameters;

    using Unity.Entities;
    using Unity.Mathematics;

    public class ComputedPositionHandle : IHandleSpec<float> {
        public string                Parent          { get; init; }
        public NT_HandleConstraints? Constraints     { get; init; }
        public float                 Radius          { get; init; } = NT_Handle.PrimaryRadius;
        public ComputePositionDelegate<float>     ComputePosition     { get; init; }
        public ComputeFromPositionDelegate<float> ComputeFromPosition { get; init; }

        HandleTypeFlags IHandleSpec.TypeFlags => HandleTypeFlags.Position;

        internal Float3Parameter ResolvedParent;

        public void SyncToEntity(NT_BaseToolSystem tool, Entity entity, ParameterBase param) {
            var value = ((FloatParameter)param).Value;
            var pos   = ComputePosition(tool, value);
            tool.EntityManager.SetComponentData(entity,
                new NT_HandlePosition { Position = pos, Rotation = quaternion.identity });
        }
    }
}
