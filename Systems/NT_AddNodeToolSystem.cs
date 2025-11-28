// <copyright file="NT_AddNodeToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;

    #endregion

    public partial class NT_AddNodeToolSystem : NT_BaseToolSystem {
        private ControlPoint      m_ControlPoint;
        private Entity            m_HoveredEntity;
        private EntityQuery       m_DefinitionQuery;
        private float3            m_LastHitPosition;
        private PrefabBase        m_Prefab;
        private SearchSystem      m_NetSearchSystem;
        private TerrainSystem     m_TerrainSystem;
        private ToolOutputBarrier m_ToolOutputBarrier;
        private WaterSystem       m_WaterSystem;

        public override bool TrySetPrefab(PrefabBase prefab) {
            m_Log.Debug($"TrySetPrefab {prefab is NT_ToolPrefab} {m_PrefabSystem.HasComponent<NT_AddDelete>(prefab)}");
            return prefab is NT_ToolPrefab && m_PrefabSystem.HasComponent<NT_AddDelete>(prefab);
        }

        protected override void OnCreate() {
            m_ToolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_TerrainSystem     = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_WaterSystem       = World.GetOrCreateSystemManaged<WaterSystem>();
            m_NetSearchSystem   = World.GetOrCreateSystemManaged<SearchSystem>();
            base.OnCreate();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            const string logPrefix = "OnUpdate()";
            var          buffer    = World.GetOrCreateSystemManaged<OverlayRenderSystem>().GetBuffer(out var bufferJobHandle);
            buffer.DrawCircle(Color.white, m_ControlPoint.m_Position, 4f);

            inputDeps = JobHandle.CombineDependencies(
                inputDeps,
                bufferJobHandle);

            // If this tool is not the active tool, clear UI state and bail out.
            if (m_ToolSystem.activeTool != this) {
                // Cleanup..
                return inputDeps;
            }

            //// Handle Actions
            //if (m_HoveredEntity != Entity.Null && m_ControlPoint.m_OriginalEntity != Entity.Null) {
            //    // Do actions
            //}

            // Handle Entity Selection
            if (!GetRaycastResult(
                    out var entity,
                    out RaycastHit raycastHit)) {
                m_HoveredEntity   = Entity.Null;
                m_LastHitPosition = float3.zero;
                m_Prefab          = null;
                return inputDeps;
            }

            var previousHoveredEntity = m_HoveredEntity;
            m_HoveredEntity   = entity;
            m_LastHitPosition = raycastHit.m_HitPosition;

            // Update hover / prefabs
            if (m_HoveredEntity != previousHoveredEntity) {
                var isEdge = EntityManager.HasComponent<Curve>(entity);

                if (!isEdge) {
                    m_Prefab = null;
                }

                var curvePrefabRef = EntityManager.GetComponentData<PrefabRef>(entity);
                m_Prefab = m_PrefabSystem.GetPrefab<PrefabBase>(curvePrefabRef);
            }

            // If we're here and have a prefab, proceed with snapping and temp entity creation
            if (m_Prefab == null) {
                return inputDeps;
            }

            // Snap our hit position to the hovered entity
            inputDeps = SnapControlPoint(inputDeps);

            // Handle Temp Entities Creation / Destruction
            inputDeps = CreateTempEntities(inputDeps);

            return inputDeps;
        }

        public override void InitializeRaycast() {
            base.InitializeRaycast();

            m_ToolRaycastSystem.collisionMask   = CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.Underground;
            m_ToolRaycastSystem.typeMask        = TypeMask.Net;
            m_ToolRaycastSystem.netLayerMask    = Layer.All;
            m_ToolRaycastSystem.netLayerMask    = Layer.All;
            m_ToolRaycastSystem.iconLayerMask   = IconLayerMask.None;
            m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;
            m_ToolRaycastSystem.raycastFlags = RaycastFlags.Markers | RaycastFlags.ElevateOffset | RaycastFlags.SubElements |
                                               RaycastFlags.Cargo   | RaycastFlags.Passenger;
        }

        private JobHandle SnapControlPoint(JobHandle inputDeps) {
            //var snapJobHandle = new SnapJob(
            //    netTree: m_NetSearchSystem.GetNetSearchTree(true, out var netTreeJobHandle),
            //    terrainHeightLookup: m_TerrainSystem.GetHeightData(),
            //    waterSurfaceLookup: m_WaterSystem.GetSurfaceData(out var waterSurfaceJobHandle),
            //    controlPoint: m_ControlPoint,
            //    entityTypeHandle: SystemAPI.GetEntityTypeHandle(),
            //    nodeLookup: SystemAPI.GetComponentLookup<Node>(),
            //    edgeLookup: SystemAPI.GetComponentLookup<Edge>(),
            //    curveLookup: SystemAPI.GetComponentLookup<Curve>(),
            //    compositionLookup: SystemAPI.GetComponentLookup<Composition>(),
            //    prefabRefLookup: SystemAPI.GetComponentLookup<PrefabRef>(),
            //    netLookupLookup: SystemAPI.GetComponentLookup<NetData>(),
            //    netGeometryLookupLookup: SystemAPI.GetComponentLookup<NetGeometryData>(),
            //    netCompositionLookupLookup: SystemAPI.GetComponentLookup<NetCompositionData>(),
            //    connectedEdgeLookup: SystemAPI.GetBufferLookup<ConnectedEdge>()
            //).Schedule(inputDeps);
            return inputDeps;
        }

        private JobHandle CreateTempEntities(JobHandle inputDeps) {
            //var destroyDefinitionsJobHandle = DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);

            //if (m_Prefab == null) {
            //    return destroyDefinitionsJobHandle;
            //}

            //var createDefinitionsJobHandle = new CreateDefinitionsJob() {
            //    m_WaterSurfaceLookup = m_WaterSystem.GetVelocitiesSurfaceData(out var waterSystemJobHandle),
            //}.Schedule(JobHandle.CombineDependencies(inputDeps, waterSystemJobHandle));

            //destroyDefinitionsJobHandle = JobHandle.CombineDependencies(destroyDefinitionsJobHandle, createDefinitionsJobHandle);


            return inputDeps;
        }
    }
}