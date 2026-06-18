namespace NetworkTools.Components.Handles {
    using Unity.Entities;

    /// <summary>
    /// Render-only link from a handle to another handle it draws a dashed connector line to.
    /// Purely visual — it does not move the handle or affect any value (see
    /// <c>IHandleSpec.RenderConnectionTo</c>). Stamped by the handle resolver from the
    /// <c>RenderConnectionTo</c> parameter reference.
    /// </summary>
    public struct NT_HandleConnector : IComponentData {
        /// <summary>
        /// The target handle entity this handle draws a connector line to.
        /// </summary>
        public Entity Target;
    }
}
