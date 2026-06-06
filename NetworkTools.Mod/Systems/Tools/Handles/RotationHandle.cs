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
        public float                 Size                { get; init; } = 16f;
        public HandleSnap            Snap                { get; init; } = HandleSnap.None;
        public ComputePositionDelegate<float3>     ComputePosition     { get; init; }
        public ComputeFromPositionDelegate<float3> ComputeFromPosition { get; init; }

        HandleTypeFlags IHandleSpec.TypeFlags => HandleTypeFlags.Rotation | Style;

        internal Float3Parameter ResolvedParent;

        public void SyncToEntity(NT_BaseToolSystem tool, Entity entity, ParameterBase param) {
            var direction = ((Float3Parameter)param).Value;
            var rotation  = tool.EntityManager.GetComponentData<NT_HandleRotation>(entity);
            var perp      = math.cross(rotation.Normal, rotation.ReferenceDirection);
            var angle     = math.atan2(math.dot(direction, perp), math.dot(direction, rotation.ReferenceDirection));
            rotation.Angle = angle;
            tool.EntityManager.SetComponentData(entity, rotation);
        }
    }
}
