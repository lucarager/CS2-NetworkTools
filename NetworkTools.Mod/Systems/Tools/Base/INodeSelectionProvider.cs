namespace NetworkTools.Systems.Tools {
    using Unity.Entities;

    /// <summary>
    ///     Implemented by tool systems that expose selected node entities to the UI.
    /// </summary>
    public interface INodeSelectionProvider {
        /// <summary>
        ///     Gets the currently selected node entities.
        /// </summary>
        Entity[] GetSelectedNodes();
    }
}
