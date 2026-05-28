namespace NetworkTools.Components {
    using Unity.Entities;

    /// <summary>
    /// Stores precomputed slope data on edge entities during the RoadShape transform pipeline.
    /// Read by the tooltip system to display accurate slope values without relying on temp entities.
    /// </summary>
    public struct NT_Metadata : IComponentData {
        public float ExistingSlope;
        public float NewSlope;
    }
}
