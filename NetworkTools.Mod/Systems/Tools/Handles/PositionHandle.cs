namespace NetworkTools.Systems.Tools.Handles {
    using System;
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

        /// <summary>
        ///     Axis-frame source for an axis-locked (bezier) handle: the line origin and direction.
        ///     When both are set, the handle is constrained to slide along that line (resolved once
        ///     at build — sources are static during editing, so no <see cref="DependsOn"/> is needed).
        ///     Same mechanism <see cref="AxisHandle"/> uses; a delegate is strictly more expressive
        ///     than the former name-reference form.
        /// </summary>
        public Func<NT_BaseToolSystem, float3> ConstraintOrigin { get; init; }
        public Func<NT_BaseToolSystem, float3> ConstraintAxis   { get; init; }

        HandleTypeFlags IHandleSpec.TypeFlags => HandleTypeFlags.Position | Style;

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
