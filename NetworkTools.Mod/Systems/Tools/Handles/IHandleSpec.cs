namespace NetworkTools.Systems.Tools.Handles {
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Parameters;
    using Unity.Entities;
    using Unity.Mathematics;

    public delegate float3 ComputePositionDelegate<in T>(NT_BaseToolSystem tool, T value);
    public delegate T      ComputeFromPositionDelegate<out T>(NT_BaseToolSystem tool, float3 worldPos);

    public interface IHandleSpec {
        HandleTypeFlags       TypeFlags    { get; }
        NT_HandleConstraints? Constraints  { get; }
        float                 Size         { get; }

        /// <summary>Declarative snap behavior for this handle (default <see cref="HandleSnap.None" />).</summary>
        HandleSnap            Snap         { get; }

        /// <summary>
        ///     Parameters whose value changes this handle's value/geometry derives from.
        ///     Each entry's reaction is the spec-type default (<see cref="OnDependencyChanged" />)
        ///     unless it carries a custom <see cref="DependencyUpdate" />. <c>null</c> = no dependencies.
        /// </summary>
        Dependency[] DependsOn { get; }

        /// <summary>
        ///     Render-only: the parameter name whose primary handle entity this handle draws a
        ///     dashed connector line to. Has no effect on value or geometry. <c>null</c> = no connector.
        ///     Deliberately independent of <see cref="DependsOn" /> (a view relationship, not data).
        /// </summary>
        string RenderConnectionTo { get; }

        void SyncToEntity(NT_BaseToolSystem tool, Entity entity, ParameterBase param);

        /// <summary>
        ///     Spec-type default reaction when a bare (delegate-less) dependency <paramref name="source" />
        ///     changes. The behaviour is decided by the spec type: position-follow translates the owner
        ///     value by <paramref name="delta" />; recenter moves the handle entity onto the source;
        ///     re-resolve re-derives position and constraints from the spec's delegates.
        /// </summary>
        void OnDependencyChanged(NT_BaseToolSystem tool, Entity entity,
                                 ParameterBase owner, Float3Parameter source, float3 delta);
    }

    public interface IHandleSpec<T> : IHandleSpec {
        ComputePositionDelegate<T>     ComputePosition     { get; }
        ComputeFromPositionDelegate<T> ComputeFromPosition { get; }
    }
}
