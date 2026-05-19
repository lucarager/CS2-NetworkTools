namespace NetworkTools.Systems.Tools {
    using System.Collections.Generic;

    using Game.Common;
    using Game.Input;
    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;

    using NetworkTools.Components;
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Generate;
    using NetworkTools.Systems.Tools.Parameters;

    using NetworkTools.Utils;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;


    /// <summary>
    ///     Prefab caching tool system
    /// </summary>
    public partial class NT_PrefabCacheToolSystem : ToolBaseSystem {
        internal  PrefixedLogger m_Log;
        protected EntityQuery    m_ContainerQuery;
        public NetPrefabParameter LastNetPrefab = new("common.lastNetPrefab");
        public override string toolID => "NT_PrefabCacheToolSystem";


        protected override void OnCreate() {
            base.OnCreate();

            // Start disabled - tools must be explicitly enabled
            Enabled = false;

            // Logging
            m_Log = new PrefixedLogger(nameof(NT_BaseToolSystem));
            m_Log.Debug("OnCreate()");

            // Move this tool to the front of the tool stack so it takes priority over vanilla tools
            m_ToolSystem.tools.Remove(this);
            m_ToolSystem.tools.Insert(0, this);

            // Vanilla container query
            m_ContainerQuery = GetContainerQuery();
        }

        public override PrefabBase GetPrefab() {
            return null;
        }

        public override bool TrySetPrefab(PrefabBase prefab) {
            m_Log.Debug($"TrySetPrefab: {prefab.name}. Cache: {prefab is RoadPrefab or TrackPrefab or WaterwayPrefab or PathwayPrefab or NetLanePrefab}");

            switch (prefab)
            {
                case RoadPrefab or TrackPrefab or WaterwayPrefab or PathwayPrefab:
                    LastNetPrefab.Set(prefab, m_PrefabSystem.GetEntity((NetPrefab)prefab), Entity.Null);
                    break;
                case NetLanePrefab netLanePrefab:
                    var laneEntity = m_PrefabSystem.GetEntity(netLanePrefab);
                    GetContainers(m_ContainerQuery, out var container, out _);
                    LastNetPrefab.Set(prefab, container, laneEntity);
                    break;
            }

            return false;
        }
    }
}