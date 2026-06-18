namespace NetworkTools.Systems.Tools.Handles {
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Parameters;
    using Unity.Entities;
    using Unity.Mathematics;

    public class PositionHandle : IHandleSpec<float3> {
        public HandleTypeFlags       Style           { get; init; }
        public Dependency[]          DependsOn          { get; init; }
        public string                RenderConnectionTo { get; init; }
        public NT_HandleConstraints? Constraints     { get; init; }
        public float                 Size          { get; init; } = NT_Handle.SizePrimary;
        public HandleSnap            Snap          { get; init; } = HandleSnap.None;
        public ComputePositionDelegate<float3>     ComputePosition     { get; init; }
        public ComputeFromPositionDelegate<float3> ComputeFromPosition { get; init; }

        public string ConstraintAxisFrom   { get; init; }
        public string ConstraintOriginFrom { get; init; }

        HandleTypeFlags IHandleSpec.TypeFlags => HandleTypeFlags.Position | Style;

        internal Float3Parameter ResolvedConstraintAxis;
        internal Float3Parameter ResolvedConstraintOrigin;

        public void SyncToEntity(NT_BaseToolSystem tool, Entity entity, ParameterBase param) {
            var value = ((Float3Parameter)param).Value;
            var pos   = ComputePosition != null ? ComputePosition(tool, value) : value;
            tool.EntityManager.SetComponentData(entity,
                new NT_HandlePosition { Position = pos });
        }

        /// <summary>
        ///     Position-follow: shift the owner value by the anchor's delta. The value-write
        ///     cascades — reverse-sync moves this handle's entity, and any grandchildren follow.
        /// </summary>
        public void OnDependencyChanged(NT_BaseToolSystem tool, Entity entity,
                                        ParameterBase owner, Float3Parameter source, float3 delta) {
            if (math.lengthsq(delta) < 1e-8f) return;
            var p = (Float3Parameter)owner;
            p.SetValue(p.Value + delta, ChangeOrigin.Dependency);
        }
    }
}
