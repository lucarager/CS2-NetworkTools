namespace NetworkTools.Systems.Tools.Handles {
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Parameters;
    using Unity.Entities;
    using Unity.Mathematics;

    public class ComputedPositionHandle : IHandleSpec<float> {
        public Dependency[]          DependsOn          { get; init; }
        public string                RenderConnectionTo { get; init; }
        public NT_HandleConstraints? Constraints     { get; set; }
        public float                 Size          { get; init; } = NT_Handle.SizePrimary;
        public HandleSnap            Snap          { get; init; } = HandleSnap.None;
        public ComputePositionDelegate<float>     ComputePosition     { get; init; }
        public ComputeFromPositionDelegate<float> ComputeFromPosition { get; init; }

        HandleTypeFlags IHandleSpec.TypeFlags => HandleTypeFlags.Position | HandleTypeFlags.AxisHandle;

        public void SyncToEntity(NT_BaseToolSystem tool, Entity entity, ParameterBase param) {
            var value = ((FloatParameter)param).Value;
            var pos   = ComputePosition(tool, value);
            tool.EntityManager.SetComponentData(entity,
                new NT_HandlePosition { Position = pos });
        }

        /// <summary>Re-resolve: re-derive the handle position from the compute delegate.</summary>
        public void OnDependencyChanged(NT_BaseToolSystem tool, Entity entity,
                                        ParameterBase owner, Float3Parameter source, float3 delta) {
            SyncToEntity(tool, entity, owner);
        }
    }
}
