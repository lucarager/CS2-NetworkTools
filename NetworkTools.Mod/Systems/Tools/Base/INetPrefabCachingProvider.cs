namespace NetworkTools.Systems.Tools {
    using Game.Prefabs;

    /// <summary>
    ///     Implemented by tool systems that cache net prefab selections from the asset panel.
    ///     The base <see cref="NT_BaseToolSystem.TrySetPrefab"/> delegates to
    ///     <see cref="TryCacheNetPrefab"/> before the standard tool-activation check.
    /// </summary>
    public interface INetPrefabCachingProvider {
        /// <summary>
        ///     Attempts to cache a net prefab selection.
        /// </summary>
        /// <param name="prefab">The prefab offered by the game.</param>
        /// <returns>
        ///     <c>true</c> to consume the prefab (tool is active and should reflect the change),
        ///     <c>false</c> to reject (prefab was cached but tool is not active),
        ///     or <c>null</c> to fall through to the standard tool-activation check.
        /// </returns>
        bool? TryCacheNetPrefab(PrefabBase prefab);
    }
}
