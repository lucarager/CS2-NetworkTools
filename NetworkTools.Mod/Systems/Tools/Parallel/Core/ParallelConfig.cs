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
        public ParallelSide Side;

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
        public float SignedHorizontalOffset => Side == ParallelSide.Right ? HorizontalOffset : -HorizontalOffset;

        public static ParallelConfig Default => new ParallelConfig {
            HorizontalOffset = DefaultDistance,
            VerticalOffset = DefaultDistance,
            Side = ParallelSide.Right
        };

        /// <inheritdoc />
        public void Write(IJsonWriter writer) {
            writer.TypeBegin(GetType().FullName);

            writer.PropertyName("horizontalOffset");
            writer.Write(HorizontalOffset);


            writer.PropertyName("verticalOffset");
            writer.Write(VerticalOffset);

            writer.PropertyName("side");
            writer.Write((int)Side);

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

            reader.ReadProperty("side");
            reader.Read(out int side);
            Side = (ParallelSide)side;

            reader.ReadMapEnd();
        }
    }
}
