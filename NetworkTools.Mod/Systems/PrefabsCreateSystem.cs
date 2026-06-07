namespace NetworkTools.Systems {
    using System.Collections.Generic;
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Prefabs;
    using NetworkTools.Components.Tools;
    using LucaModsCommon.Utils;
    using Unity.Entities;
    using UnityEngine;

    public partial class NT_PrefabsCreateSystem : GameSystemBase {
        /// <summary>
        ///     Stateful value to only run installation once.
        /// </summary>
        private static bool m_PrefabsAreInstalled;

        /// <summary>
        ///     PrefabSystem
        /// </summary>
        private static PrefabSystem m_PrefabSystem;

        /// <summary>
        ///     Gets or sets the number of installed tools.
        /// </summary>
        private int m_InstalledTools;

        /// <summary>
        ///     Logger instance
        /// </summary>
        private PrefixedLogger m_Log;

        /// <summary>
        ///     Cache for prefabs.
        /// </summary>
        private List<PrefabBase> m_PrefabBases;

        /// <summary>
        ///     Cache for prefab entities.
        /// </summary>
        private Dictionary<PrefabBase, Entity> m_PrefabEntities;

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();
            m_Log = new PrefixedLogger(nameof(NT_PrefabsCreateSystem));
            m_Log.Debug("OnCreate()");
            m_PrefabBases    = new List<PrefabBase>();
            m_PrefabEntities = new Dictionary<PrefabBase, Entity>();
            m_PrefabSystem   = World.GetOrCreateSystemManaged<PrefabSystem>();
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
        }

        /// <inheritdoc />
        protected override void OnGameLoadingComplete(Purpose  purpose,
                                                      GameMode mode) {
            base.OnGameLoadingComplete(purpose, mode);
            m_Log.Debug($"OnGameLoadingComplete(purpose={purpose}, mode={mode})");

            if (!m_PrefabsAreInstalled) {
                Install();
            }
        }

        /// <inheritdoc />
        protected override void OnGamePreload(Purpose purpose, GameMode mode) {
            base.OnGamePreload(purpose, mode);
            var logMethodPrefix = $"OnGamePreload(purpose {purpose}, mode {mode}) --";
            m_Log.Debug($"{logMethodPrefix}");

            if (!m_PrefabsAreInstalled) {
                Install();
            }
        }

        private void Install() {
            var logMethodPrefix = "Install() --";

            // Mark the Install as already _prefabsAreInstalled
            m_PrefabsAreInstalled = true;

            CreateToolPrefab("AddNode",    "add.svg",      "node",       new NT_AddNodeTool(),    IsToolActive(true));
            CreateToolPrefab("RemoveNode", "remove.svg",   "node",       new NT_RemoveNodeTool(), IsToolActive(true));
            CreateToolPrefab("SlideNode",  "move.svg",     "node",       new NT_SlideNodeTool(),  IsToolActive(false));
            CreateToolPrefab("SuperNode",  "super.svg",    "node",       new NT_SuperNodeTool(),  IsToolActive(true));
            CreateToolPrefab("ShapeSlope", "slope.svg",    "shape",      new NT_ShapeSlopeTool(), IsToolActive(true));
            CreateToolPrefab("ShapeCurve", "curve.svg",    "shape",      new NT_ShapeCurveTool(), IsToolActive(true));
            CreateToolPrefab("Connect",    "connect.svg",  "generative", new NT_ConnectTool(),    IsToolActive(true));
            CreateToolPrefab("Parallel",   "parallel.svg", "generative", new NT_ParallelTool(),   IsToolActive(true));
            CreateToolPrefab("Generate",   "generate.svg", "generative", new NT_GenerateTool(),   IsToolActive(true));

            m_Log.Debug($"{logMethodPrefix} Completed.");
        }

        /// <summary>
        ///     Returns true in debug builds (all tools active), or the release flag otherwise.
        /// </summary>
        private static bool IsToolActive(bool releaseActive) {
#if IS_DEBUG
            return true;
#else
            return releaseActive;
#endif
        }

        private bool CreateToolPrefab<T>(string id, string icon, string category, T component,
                                         bool   active)
            where T : unmanaged, IComponentData {
            var toolPrefabBase = ScriptableObject.CreateInstance<NT_ToolPrefab>();
            toolPrefabBase.name        = id;
            toolPrefabBase.Id          = id;
            toolPrefabBase.DisplayName = $"NetworkTools.Tools.{id}.Name";
            toolPrefabBase.Description = $"NetworkTools.Tools.{id}.Description";
            toolPrefabBase.Icon        = icon;
            toolPrefabBase.Category    = category;
            toolPrefabBase.Active      = active;
            toolPrefabBase.Index       = m_InstalledTools++;

            var success = m_PrefabSystem.AddPrefab(toolPrefabBase);

            if (success) {
                var prefabEntity = m_PrefabSystem.GetEntity(toolPrefabBase);
                EntityManager.AddComponentData(prefabEntity, component);
                m_PrefabBases.Add(toolPrefabBase);
                m_PrefabEntities.Add(toolPrefabBase, prefabEntity);
            }

            return success;
        }
    }
}
