namespace NetworkTools.Systems.Tools.Parameters {
    public enum ChangeOrigin {
        Code,
        Handle,

        /// <summary>
        ///     The value was written by dependency propagation (a follow/mirror reacting to a
        ///     source parameter). Distinguished from <see cref="Code" /> so dependency-driven
        ///     writes are identifiable; the per-pass visited set is the primary cycle guard.
        /// </summary>
        Dependency
    }
}
