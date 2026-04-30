namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.UI.Binding;
    using Unity.Mathematics;

    /// <summary>
    ///     Holds generation parameters and control point positions.
    /// </summary>
    public struct GenerateConfig : IJsonWritable, IJsonReadable {
        // Shared
        public float3 StartPosition;
        public quaternion StartDirection;

        // Grid
        public float GridXSpacing;
        public float GridZSpacing;
        public int GridXNum = 1;
        public int GridZNum = 1;
        public const float GridDefaultXSpacing = 80f;
        public const float GridDefaultZSpacing = 80f;
        public const float GridMinSpacing = 4f;
        public const float GridMaxSpacing = 500f;
        public const int GridDefaultXNum = 2;
        public const int GridDefaultZNum = 2;

        // Circle

        public GenerateConfig(float3 startPosition, quaternion startDirection) {
            StartPosition = startPosition;
            StartDirection = startDirection;

            // Grid defaults
            GridXSpacing = GridDefaultXSpacing;
            GridZSpacing = GridDefaultZSpacing;
            GridXNum = GridDefaultXNum;
            GridZNum = GridDefaultZNum;

            // Circle Defaults
        }

        /// <inheritdoc />
        public void Write(IJsonWriter writer) {
            writer.TypeBegin(GetType().FullName);

            writer.PropertyName("gridXSpacing");
            writer.Write(GridXSpacing);

            writer.PropertyName("gridZSpacing");
            writer.Write(GridZSpacing);

            writer.PropertyName("gridXNum");
            writer.Write(GridXNum);

            writer.PropertyName("gridZNum");
            writer.Write(GridZNum);


            writer.TypeEnd();
        }

        /// <inheritdoc />
        public void Read(IJsonReader reader) {
            reader.ReadMapBegin();

            reader.ReadProperty("gridXSpacing");
            reader.Read(out float gridXSpacing);
            GridXSpacing = gridXSpacing;

            reader.ReadProperty("gridZSpacing");
            reader.Read(out float gridZSpacing);
            GridZSpacing = gridZSpacing;

            reader.ReadProperty("gridXNum");
            reader.Read(out int gridXNum);
            GridXNum = gridXNum;

            reader.ReadProperty("gridZNum");
            reader.Read(out int gridZNum);
            GridZNum = gridZNum;

            reader.ReadMapEnd();
        }
    }
}
