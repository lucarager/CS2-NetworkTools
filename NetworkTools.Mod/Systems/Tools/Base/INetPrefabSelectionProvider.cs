namespace NetworkTools.Systems.Tools {
    using Game.Prefabs;
    using Unity.Entities;

    /// <summary>
    ///     Implemented by tool systems that have a selected net prefab the UI should reflect.
    /// </summary>
    public interface INetPrefabSelectionProvider {
        /// <summary>
        ///     Gets the currently selected net prefab, or <c>null</c> if none is selected.
        /// </summary>
        NetPrefab SelectedNetPrefab { get; }

        /// <summary>
        ///     Gets the ECS entity of the selected net prefab, or <see cref="Entity.Null" /> if none.
        /// </summary>
        Entity SelectedNetPrefabEntity { get; }
    }
}
