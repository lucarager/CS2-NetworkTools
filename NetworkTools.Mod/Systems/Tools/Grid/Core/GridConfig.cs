namespace NetworkTools.Systems.Tools {
    using Colossal.UI.Binding;

    using Unity.Mathematics;

    /// <summary>
    ///     Holds grid generation parameters and control point positions.
    /// </summary>
    public struct GridConfig : IJsonWritable, IJsonReadable {
        /// <summary>
        ///     First control point position (grid origin).
        /// </summary>
        public float3 StartPosition;

        /// <summary>
        ///     Second control point position (defines initial direction).
        /// </summary>
        public float3 EndPosition;

        /// <summary>
        ///     Grid rotation angle in degrees, initially derived from the two control points.
        /// </summary>
        public float Angle;

        /// <summary>
        ///     Spacing between grid lines along the primary (X) axis.
        /// </summary>
        public float XSpacing;

        /// <summary>
        ///     Spacing between grid lines along the secondary (Y) axis.
        /// </summary>
        public float YSpacing;

        /// <summary>
        ///     Default X spacing value in world units.
        /// </summary>
        public const float DefaultXSpacing = 80f;

        /// <summary>
        ///     Default Y spacing value in world units.
        /// </summary>
        public const float DefaultYSpacing = 80f;

        /// <summary>
        ///     Minimum spacing value in world units.
        /// </summary>
        public const float MinSpacing = 10f;

        /// <summary>
        ///     Maximum spacing value in world units.
        /// </summary>
        public const float MaxSpacing = 500f;

        public GridConfig(float3 startPosition, float3 endPosition) {
            StartPosition = startPosition;
            EndPosition   = endPosition;

            // Calculate angle from the direction between the two control points
            var delta = endPosition - startPosition;
            delta.y = 0f;
            Angle = math.degrees(math.atan2(delta.z, delta.x));

            XSpacing = DefaultXSpacing;
            YSpacing = DefaultYSpacing;
        }

        /// <inheritdoc />
        public void Write(IJsonWriter writer) {
            writer.TypeBegin(GetType().FullName);

            writer.PropertyName("angle");
            writer.Write(Angle);

            writer.PropertyName("xSpacing");
            writer.Write(XSpacing);

            writer.PropertyName("ySpacing");
            writer.Write(YSpacing);

            writer.TypeEnd();
        }

        /// <inheritdoc />
        public void Read(IJsonReader reader) {
            reader.ReadMapBegin();

            reader.ReadProperty("angle");
            reader.Read(out float angle);
            Angle = angle;

            reader.ReadProperty("xSpacing");
            reader.Read(out float xSpacing);
            XSpacing = xSpacing;

            reader.ReadProperty("ySpacing");
            reader.Read(out float ySpacing);
            YSpacing = ySpacing;

            reader.ReadMapEnd();
        }
    }
}
