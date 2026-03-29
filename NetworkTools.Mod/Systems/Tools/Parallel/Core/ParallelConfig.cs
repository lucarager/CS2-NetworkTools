namespace NetworkTools.Systems.Tools.Parallel {
    using Colossal.UI.Binding;

    /// <summary>
    ///     Determines which side of the original path the parallel copy is placed on.
    /// </summary>
    public enum ParallelSide {
        /// <summary>Offset to the left of the path direction.</summary>
        Left = 0,

        /// <summary>Offset to the right of the path direction.</summary>
        Right = 1
    }

    /// <summary>
    ///     Determines whether the vertical offset goes up or down.
    /// </summary>
    public enum VerticalSide {
        /// <summary>Offset upward.</summary>
        Up = 0,

        /// <summary>Offset downward.</summary>
        Down = 1
    }

    /// <summary>
    ///     Holds configuration for parallel road generation: offset distance and side.
    /// </summary>
    public struct ParallelConfig : IJsonWritable, IJsonReadable {
        /// <summary>
        ///     Perpendicular offset distance in world units.
        /// </summary>
        public float HorizontalOffset;

        /// <summary>
        ///     Vertical offset distance in world units.
        /// </summary>
        public float VerticalOffset;

        /// <summary>
        ///     Which side of the original path to place the parallel copy.
        /// </summary>
        public ParallelSide HorizontalDirection;

        /// <summary>
        ///     Whether the vertical offset goes up or down.
        /// </summary>
        public VerticalSide VerticalDirection;

        /// <summary>
        ///     Whether to reverse the direction of the created network.
        /// </summary>
        public bool ReverseDirection;

        /// <summary>
        ///     Default offset distance in world units.
        /// </summary>
        public const float DefaultDistance = 20f;

        /// <summary>
        ///     Minimum offset distance in world units.
        /// </summary>
        public const float MinDistance = 0f;

        /// <summary>
        ///     Maximum offset distance in world units.
        /// </summary>
        public const float MaxDistance = 80f;

        /// <summary>
        ///     Returns the signed offset based on the selected side.
        ///     Positive = right, Negative = left (following standard road conventions).
        /// </summary>
        public float SignedHorizontalOffset => HorizontalDirection == ParallelSide.Right ? HorizontalOffset : -HorizontalOffset;

        /// <summary>
        ///     Returns the signed vertical offset based on the selected direction.
        ///     Positive = up, Negative = down.
        /// </summary>
        public float SignedVerticalOffset => VerticalDirection == VerticalSide.Up ? VerticalOffset : -VerticalOffset;

        public static ParallelConfig Default => new ParallelConfig {
            HorizontalOffset = DefaultDistance,
            VerticalOffset = DefaultDistance,
            HorizontalDirection = ParallelSide.Right,
            VerticalDirection = VerticalSide.Up,
            ReverseDirection = false
        };

        /// <inheritdoc />
        public void Write(IJsonWriter writer) {
            writer.TypeBegin(GetType().FullName);

            writer.PropertyName("horizontalOffset");
            writer.Write(HorizontalOffset);


            writer.PropertyName("verticalOffset");
            writer.Write(VerticalOffset);

            writer.PropertyName("horizontalDirection");
            writer.Write((int)HorizontalDirection);

            writer.PropertyName("verticalDirection");
            writer.Write((int)VerticalDirection);

            writer.PropertyName("reverseDirection");
            writer.Write(ReverseDirection);

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

            reader.ReadProperty("horizontalDirection");
            reader.Read(out int horizontalDirection);
            HorizontalDirection = (ParallelSide)horizontalDirection;

            reader.ReadProperty("verticalDirection");
            reader.Read(out int verticalDirection);
            VerticalDirection = (VerticalSide)verticalDirection;

            reader.ReadProperty("reverseDirection");
            reader.Read(out bool reverseDirection);
            ReverseDirection = reverseDirection;

            reader.ReadMapEnd();
        }
    }
}
