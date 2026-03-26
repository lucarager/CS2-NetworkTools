namespace NetworkTools.Systems.Tools {
    using Game.Prefabs;

    /// <summary>
    ///     Implemented by tool systems to declare which prefab component activates them.
    ///     When implemented, <see cref="NT_BaseToolSystem.TrySetPrefab"/> validates the prefab
    ///     and sets <c>m_Prefab</c> automatically.
    /// </summary>
    public interface IToolPrefabProvider {
        /// <summary>
        ///     Returns whether the given prefab has the required tool component for activation.
        /// </summary>
        /// <param name="prefab">The prefab to check.</param>
        bool HasToolComponent(PrefabBase prefab);
    }
}
