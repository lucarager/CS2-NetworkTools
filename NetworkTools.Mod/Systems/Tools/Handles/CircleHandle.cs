namespace NetworkTools.Systems.Tools.Handles {
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Parameters;
    using Unity.Entities;
    using Unity.Mathematics;

    public class CircleHandle : IHandleSpec<float> {
        public Dependency[]          DependsOn          { get; init; }
        public string                RenderConnectionTo { get; init; }
        public float3                Normal          { get; init; } = new(0, 1, 0);
        public NT_HandleConstraints? Constraints     { get; init; }
        public float                 Size          { get; init; } = NT_Handle.SizePrimary;
        public HandleSnap            Snap          { get; init; } = HandleSnap.None;
        public ComputePositionDelegate<float>     ComputePosition     { get; init; }
        public ComputeFromPositionDelegate<float> ComputeFromPosition { get; init; }

        HandleTypeFlags IHandleSpec.TypeFlags => HandleTypeFlags.Circle;

        public void SyncToEntity(NT_BaseToolSystem tool, Entity entity, ParameterBase param) {
            var radius = ((FloatParameter)param).Value;
            var circle = tool.EntityManager.GetComponentData<NT_HandleCircle>(entity);
            circle.Radius = radius;
            tool.EntityManager.SetComponentData(entity, circle);
        }

        /// <summary>Recenter: a circle handle's center is its anchor; copy the source position onto the entity.</summary>
        public void OnDependencyChanged(NT_BaseToolSystem tool, Entity entity,
                                        ParameterBase owner, Float3Parameter source, float3 delta) {
            tool.EntityManager.SetComponentData(entity, new NT_HandlePosition { Position = source.Value });
        }
    }
}
