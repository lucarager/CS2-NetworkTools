namespace NetworkTools.Systems {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Colossal.Logging;

    using Game;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;

    using NetworkTools.Components;
    using NetworkTools.Utils;

    using Unity.Collections;
    using Unity.Entities;

    public partial class NT_PostProcessingSystem : GameSystemBase {

        /// <summary>
        ///     Logger instance
        /// </summary>
        private PrefixedLogger m_Log;
        private EntityQuery m_NodesToProcessQuery;

        protected override void OnCreate() {
            base.OnCreate();

            m_NodesToProcessQuery = SystemAPI.QueryBuilder().WithAll<NT_PostProcess, Node>().Build();

            RequireForUpdate(m_NodesToProcessQuery);
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            foreach (var nodeEntity in m_NodesToProcessQuery.ToEntityArray(Allocator.Temp)) {
                var connectedEdges = EntityManager.GetBuffer<ConnectedEdge>(nodeEntity);

                if (connectedEdges.Length == 0) {
                    EntityManager.AddComponent<Deleted>(nodeEntity);
                }
            }
        }
    }
}
