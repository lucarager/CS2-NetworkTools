namespace NetworkTools.Systems.Tools.Parallel {
    using Colossal.UI.Binding;

    /// <summary>
    ///     Holds configuration for parallel road generation with signed offsets.
    ///     Horizontal: negative = right, positive = left.
    ///     Vertical: negative = down, positive = up.
    /// </summary>
    public struct ParallelConfig : IJsonWritable, IJsonReadable {
        /// <summary>
        ///     Signed perpendicular offset distance in world units.
        ///     Negative = right of travel direction, Positive = left.
        /// </summary>
        public float HorizontalOffset;

        /// <summary>
        ///     Signed vertical offset distance in world units.
        ///     Negative = down, Positive = up.
        /// </summary>
        public float VerticalOffset;

        /// <summary>
        ///     Default offset distance in world units.
        /// </summary>
        public const float DefaultDistance = 20f;

        /// <summary>
        ///     Minimum offset distance in world units (negative bound).
        /// </summary>
        public const float MinDistance = -80f;

        /// <summary>
        ///     Maximum offset distance in world units (positive bound).
        /// </summary>
        public const float MaxDistance = 80f;

        public static ParallelConfig Default => new ParallelConfig {
            HorizontalOffset = DefaultDistance,
            VerticalOffset = 0f
        };

        /// <inheritdoc />
        public void Write(IJsonWriter writer) {
            writer.TypeBegin(GetType().FullName);

            writer.PropertyName("horizontalOffset");
            writer.Write(HorizontalOffset);

            writer.PropertyName("verticalOffset");
            writer.Write(VerticalOffset);

            writer.TypeEnd();
        }

        /// <inheritdoc />
        public void Read(IJsonReader reader) {
            reader.ReadMapBegin();

            reader.ReadProperty("horizontalOffset");
            reader.Read(out float horizontalOffset);
            HorizontalOffset = horizontalOffset;

            reader.ReadProperty("verticalOffset");
            reader.Read(out float verticalOffset);
            VerticalOffset = verticalOffset;

            reader.ReadMapEnd();
        }
    }
}
