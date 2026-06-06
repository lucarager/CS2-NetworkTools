namespace NetworkTools.Systems.Tools.Handles {
    /// <summary>
    ///     Declarative snap behavior for a handle. Carried on <see cref="IHandleSpec" /> and
    ///     consumed by the base tool's drag pipeline. The default value
    ///     (<see cref="None" />) means the handle does not snap — snapping is opt-in.
    ///     <para>
    ///     Two independent tiers can be combined:
    ///     <list type="bullet">
    ///         <item><b>World snap</b> (<see cref="World" />) — runs the shared spatial snap job
    ///             (geometry / objects / zone grid / guide lines), masked by the player's
    ///             selected snaps intersected with <see cref="WorldMask" />.</item>
    ///         <item><b>Value snap</b> (<see cref="Increment" />) — cheap quantization applied
    ///             when no world snap won, in the handle's natural space (position, distance
    ///             along an axis, radius, or angle).</item>
    ///     </list>
    ///     </para>
    /// </summary>
    public readonly struct HandleSnap {
        /// <summary>Run the shared spatial snap job, masked by the player's selected snaps.</summary>
        public bool World { get; init; }

        /// <summary>Further restricts which world snaps this handle accepts (default = all).</summary>
        public SnapOption WorldMask { get; init; }

        /// <summary>Value-space quantization step. Zero disables increment snapping.</summary>
        public float Increment { get; init; }

        /// <summary>Offset applied to the increment grid (passed to <c>MathUtils.Snap</c>).</summary>
        public float IncrementOffset { get; init; }

        /// <summary>No snapping (the default). Handles must opt in explicitly.</summary>
        public static readonly HandleSnap None = default;

        /// <summary>
        ///     Snap to world geometry per the player's selection. The most common case for
        ///     free position handles (e.g. a control point re-drag).
        /// </summary>
        public static HandleSnap WorldSnap(SnapOption mask = SnapOption.All) =>
            new() { World = true, WorldMask = mask };

        /// <summary>
        ///     Always quantize the handle's value to <paramref name="step" /> increments
        ///     (e.g. 8f cell steps), regardless of the player's snap selection.
        /// </summary>
        public static HandleSnap Grid(float step, float offset = 0f) =>
            new() { Increment = step, IncrementOffset = offset };

        /// <summary>
        ///     World snap first; when nothing wins, fall back to quantizing to
        ///     <paramref name="step" /> increments.
        /// </summary>
        public static HandleSnap WorldThenGrid(float step, SnapOption mask = SnapOption.All) =>
            new() { World = true, WorldMask = mask, Increment = step };
    }
}
