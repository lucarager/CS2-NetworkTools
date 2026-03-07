namespace NetworkTools.Components.Handles {
    using Unity.Entities;

    /// <summary>
    /// Optional component linking a child handle to its parent handle.
    /// When the parent is dragged, the child moves by the same delta.
    /// </summary>
    public struct NT_HandleParent : IComponentData {
        /// <summary>
        /// The parent handle entity. When this entity moves, the child follows.
        /// </summary>
        public Entity Parent;
    }
}
