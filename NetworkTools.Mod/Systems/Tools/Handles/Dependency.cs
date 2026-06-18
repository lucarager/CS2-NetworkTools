namespace NetworkTools.Systems.Tools.Handles {
    using NetworkTools.Systems.Tools.Parameters;

    /// <summary>
    ///     Custom per-source reaction for a <see cref="Dependency" />. When attached, it runs
    ///     instead of the spec-type default (<see cref="IHandleSpec.OnDependencyChanged" />).
    ///     The owner's value/geometry is derived from <paramref name="source" />; write the owner
    ///     with <see cref="ChangeOrigin.Dependency" /> so the write is identifiable to the cycle guard.
    /// </summary>
    public delegate void DependencyUpdate(NT_BaseToolSystem tool, ParameterBase owner, ParameterBase source);

    /// <summary>
    ///     A single declared input a handle spec's value or geometry derives from.
    ///     A <b>bare</b> entry (no <see cref="Update" />) runs the spec-type default
    ///     (<see cref="IHandleSpec.OnDependencyChanged" />) — position-follow, recenter, or
    ///     re-resolve depending on the spec type. An entry carrying an <see cref="Update" />
    ///     delegate runs that instead, so different sources of one handle can react differently.
    ///     <para>
    ///     A bare <c>string</c> source name converts implicitly to a <see cref="Dependency" />, so
    ///     the common case stays a terse list:
    ///     <c>DependsOn = new Dependency[]{ nameof(Position), nameof(Rotation) }</c>.
    ///     </para>
    /// </summary>
    public readonly struct Dependency {
        /// <summary>The source parameter's field name (resolved against the tool's parameters).</summary>
        public string Source { get; }

        /// <summary>Custom reaction; <c>null</c> means use the spec-type default.</summary>
        public DependencyUpdate Update { get; }

        public Dependency(string source, DependencyUpdate update = null) {
            Source = source;
            Update = update;
        }

        /// <summary>Bare-name sugar: a source name alone declares a default (delegate-less) dependency.</summary>
        public static implicit operator Dependency(string source) => new(source);
    }
}
