namespace NetworkTools.Systems.Tools.Handles {
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Parameters;
    using Unity.Entities;
    using Unity.Mathematics;

    public class RotationHandle : IHandleSpec<float3> {
        public HandleTypeFlags       Style               { get; init; }
        public string                Parent              { get; init; }
        public float3                Normal              { get; init; } = new(0, 1, 0);
        public string                NormalFrom          { get; init; }
        public float3                ReferenceDirection  { get; init; } = new(1, 0, 0);
        public string                ReferenceDirectionFrom { get; init; }
        public NT_HandleConstraints? Constraints         { get; init; }
        public float                 Size              { get; init; } = NT_Handle.SizePrimary;
        public ComputePositionDelegate<float3>     ComputePosition     { get; init; }
        public ComputeFromPositionDelegate<float3> ComputeFromPosition { get; init; }

        HandleTypeFlags IHandleSpec.TypeFlags => HandleTypeFlags.Rotation | Style;

        internal Float3Parameter ResolvedParent;

        public void SyncToEntity(NT_BaseToolSystem tool, Entity entity, ParameterBase param) {
            var direction = ((Float3Parameter)param).Value;
            var normal    = tool.EntityManager.GetComponentData<NT_HandleCircle>(entity).Normal;
            var refDir    = tool.EntityManager.GetComponentData<NT_HandleRotation>(entity).ReferenceDirection;
            var perp      = math.cross(normal, refDir);
            var angle     = math.atan2(math.dot(direction, perp), math.dot(direction, refDir));
            tool.EntityManager.SetComponentData(entity, NT_HandleRotation.Create(refDir, angle));
        }
    }
}
