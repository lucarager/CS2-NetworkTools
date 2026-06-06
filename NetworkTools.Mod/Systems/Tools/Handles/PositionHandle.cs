namespace NetworkTools.Systems.Tools.Handles {
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Parameters;
    using Unity.Entities;
    using Unity.Mathematics;

    public class PositionHandle : IHandleSpec<float3> {
        public HandleTypeFlags       Style           { get; init; }
        public string                Parent          { get; init; }
        public NT_HandleConstraints? Constraints     { get; init; }
        public float                 Size          { get; init; } = NT_Handle.SizePrimary;
        public HandleSnap            Snap          { get; init; } = HandleSnap.None;
        public ComputePositionDelegate<float3>     ComputePosition     { get; init; }
        public ComputeFromPositionDelegate<float3> ComputeFromPosition { get; init; }

        public string ConstraintAxisFrom   { get; init; }
        public string ConstraintOriginFrom { get; init; }

        HandleTypeFlags IHandleSpec.TypeFlags => HandleTypeFlags.Position | Style;

        internal Float3Parameter ResolvedParent;
        internal Float3Parameter ResolvedConstraintAxis;
        internal Float3Parameter ResolvedConstraintOrigin;

        public void SyncToEntity(NT_BaseToolSystem tool, Entity entity, ParameterBase param) {
            var value = ((Float3Parameter)param).Value;
            var pos   = ComputePosition != null ? ComputePosition(tool, value) : value;
            tool.EntityManager.SetComponentData(entity,
                new NT_HandlePosition { Position = pos });
        }
    }
}
