namespace NetworkTools.Systems {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Colossal.Entities;
    using Colossal.Logging;

    using Game;
    using Game.Buildings;
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
        private EntityQuery          m_EntitiesToProcessQuery;
        private EntityArchetype      m_RoadConnectionEventArchetype;
        private ModificationBarrier4 m_ModificationBarrier4;

        protected override void OnCreate() {
            base.OnCreate();

            m_ModificationBarrier4 = World.GetOrCreateSystemManaged<ModificationBarrier4>();

            m_EntitiesToProcessQuery = SystemAPI.QueryBuilder().WithAll<NT_PostProcess>().Build();

            m_RoadConnectionEventArchetype = base.EntityManager.CreateArchetype(new ComponentType[]
{
                ComponentType.ReadWrite<Event>(),
                ComponentType.ReadWrite<RoadConnectionUpdated>()
});

            RequireForUpdate(m_EntitiesToProcessQuery);
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            var ECB = m_ModificationBarrier4.CreateCommandBuffer();

            foreach (var nodeEntity in m_EntitiesToProcessQuery.ToEntityArray(Allocator.Temp)) {
                var postProcess = EntityManager.GetComponentData<NT_PostProcess>(nodeEntity);

                switch (postProcess.Operation) {
                    case NT_PostProcessOperation.DeleteNode:
                        if (EntityManager.TryGetBuffer<ConnectedEdge>(nodeEntity, true, out var connectedEdges) && 
                            connectedEdges.Length == 0) {
                            ECB.AddComponent<Deleted>(nodeEntity);
                        }

                        break;

                    case NT_PostProcessOperation.DeleteEdge:
                        break;

                    case NT_PostProcessOperation.UpdateEdge:
                        break;
                }

                EntityManager.RemoveComponent<NT_PostProcess>(nodeEntity);
            }
        }
    }
}
