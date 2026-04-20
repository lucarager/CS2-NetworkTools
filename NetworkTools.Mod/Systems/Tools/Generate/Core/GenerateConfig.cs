namespace NetworkTools.Systems.Tools {
    using Colossal.UI.Binding;

    using Unity.Mathematics;

    /// <summary>
    ///     Holds generation parameters and control point positions.
    /// </summary>
    public struct GenerateConfig : IJsonWritable, IJsonReadable {
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
        ///     Spacing between grid lines along the secondary (Z) axis.
        /// </summary>
        public float ZSpacing;

        public int XNum = 1;

        public int ZNum = 1;

        /// <summary>
        ///     Default X spacing value in world units.
        /// </summary>
        public const float DefaultXSpacing = 80f;

        /// <summary>
        ///     Default Y spacing value in world units.
        /// </summary>
        public const float DefaultZSpacing = 80f;

        /// <summary>
        ///     Minimum spacing value in world units.
        /// </summary>
        public const float MinSpacing = 4f;

        public const int DefaultXNum = 2;
        public const int DefaultZNum = 2;

        /// <summary>
        ///     Maximum spacing value in world units.
        /// </summary>
        public const float MaxSpacing = 500f;

        public GenerateConfig(float3 startPosition, float3 endPosition) {
            StartPosition = startPosition;
            EndPosition   = endPosition;

            // Calculate angle from the direction between the two control points
            var delta = endPosition - startPosition;
            delta.y = 0f;
            Angle = math.degrees(math.atan2(delta.z, delta.x));

            XSpacing = DefaultXSpacing;
            ZSpacing = DefaultZSpacing;
            XNum = DefaultXNum;
            ZNum = DefaultZNum;
        }

        /// <inheritdoc />
        public void Write(IJsonWriter writer) {
            writer.TypeBegin(GetType().FullName);

            writer.PropertyName("angle");
            writer.Write(Angle);

            writer.PropertyName("xSpacing");
            writer.Write(XSpacing);

            writer.PropertyName("zSpacing");
            writer.Write(ZSpacing);

            writer.PropertyName("xNum");
            writer.Write(XNum);

            writer.PropertyName("zNum");
            writer.Write(ZNum);
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

            reader.ReadProperty("zSpacing");
            reader.Read(out float zSpacing);
            ZSpacing = zSpacing;

            reader.ReadProperty("xNum");
            reader.Read(out int xNum);
            XNum = xNum;

            reader.ReadProperty("zNum");
            reader.Read(out int zNum);
            ZNum = zNum;

            reader.ReadMapEnd();
        }
    }
}
