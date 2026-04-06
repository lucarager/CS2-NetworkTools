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
    using Game.Simulation;

    using NetworkTools.Components;
    using NetworkTools.Utils;

    using Unity.Collections;
    using Unity.Entities;

    using static Game.Prefabs.VehicleSelectRequirementData;
    using static UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData;

    public partial class NT_DebugSystem : GameSystemBase {

        /// <summary>
        ///     Logger instance
        /// </summary>
        private PrefixedLogger m_Log;
        private EntityQuery m_EntitiesToProcessQuery;
        private EntityArchetype m_RoadConnectionEventArchetype;

        protected override void OnCreate() {
            base.OnCreate();

            m_Log = new PrefixedLogger(nameof(NT_DebugSystem));

            m_EntitiesToProcessQuery = SystemAPI.QueryBuilder().WithAll<Game.Net.ElectricityConnection, ElectricityNodeConnection, Game.Net.Edge, PrefabRef> ().Build();

            RequireForUpdate(m_EntitiesToProcessQuery);
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            foreach (var entity in m_EntitiesToProcessQuery.ToEntityArray(Allocator.Temp))
            {
                var edge = EntityManager.GetComponentData<Edge>(entity);
                var connectedNodes = EntityManager.GetBuffer<ConnectedNode>(entity);
                var prefabRef = EntityManager.GetComponentData<PrefabRef>(entity);
                var electricityNodeConnection = EntityManager.GetComponentData<ElectricityNodeConnection>(entity);

                var start = edge.m_Start;
                var end = edge.m_End;

                var electricityNode1 = EntityManager.GetComponentData<ElectricityNodeConnection>(start).m_ElectricityNode;
                var electricityNode2 = EntityManager.GetComponentData<ElectricityNodeConnection>(end).m_ElectricityNode;
                var electricityNode3 = electricityNodeConnection.m_ElectricityNode;

                if (connectedNodes.Length != 0)
                {
                    CheckNodes(entity, connectedNodes, electricityNode3);
                }
            }
        }

        private void CheckNodes(Entity edgeEntity, DynamicBuffer<ConnectedNode> connectedNodes,
        Entity flowMiddleNode) {
            foreach (var connectedNode in connectedNodes)
            {
                if (EntityManager.TryGetComponent<PrefabRef>(connectedNode.m_Node, out var prefabRef) &&
                    EntityManager.TryGetComponent<ElectricityConnectionData>(prefabRef.m_Prefab,
                                                                              out var electricityConnectionData) &&
                    EntityManager.TryGetComponent<ElectricityNodeConnection>(connectedNode.m_Node,
                                                                              out var electricityNodeConnection)) {

                    if (FindBrokenFlowEdgeBuffer(electricityNodeConnection.m_ElectricityNode,
                                  flowMiddleNode,
                                  electricityConnectionData)) {
                        m_Log.Debug($"Broken ElectricityFlowEdge found on edge {edgeEntity} for start node {electricityNodeConnection.m_ElectricityNode} with end node {flowMiddleNode}");
                    }

                }
            }
        }

        private bool FindBrokenFlowEdgeBuffer(Entity                    startNode,
                               Entity                    endNode,
                               ElectricityConnectionData connectionData) {
            // Check everything that could be broken here
            if (startNode.Index <= 0 ||
                endNode.Index <= 0 ||
                !EntityManager.HasBuffer<ConnectedFlowEdge>(startNode))
            {
                return true;
            }

            return false;
        }
    }
}
