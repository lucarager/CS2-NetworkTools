namespace NetworkTools.Systems.Tools.Handles {
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Parameters;
    using Unity.Entities;
    using Unity.Mathematics;

    public class RotationHandle : IHandleSpec<float3> {
        public HandleTypeFlags       Style               { get; init; }
        public Dependency[]          DependsOn              { get; init; }
        public string                RenderConnectionTo     { get; init; }
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

        public void SyncToEntity(NT_BaseToolSystem tool, Entity entity, ParameterBase param) {
            var direction = ((Float3Parameter)param).Value;
            var rotation  = tool.EntityManager.GetComponentData<NT_HandleRotation>(entity);
            var perp      = math.cross(rotation.Normal, rotation.ReferenceDirection);
            var angle     = math.atan2(math.dot(direction, perp), math.dot(direction, rotation.ReferenceDirection));
            rotation.Angle = angle;
            tool.EntityManager.SetComponentData(entity, rotation);
        }

        /// <summary>
        ///     Recenter: a rotation handle stores a direction, so the anchor's position delta must
        ///     not shift its value — instead move the handle entity's center onto the source.
        /// </summary>
        public void OnDependencyChanged(NT_BaseToolSystem tool, Entity entity,
                                        ParameterBase owner, Float3Parameter source, float3 delta) {
            tool.EntityManager.SetComponentData(entity, new NT_HandlePosition { Position = source.Value });
        }
    }
}
