// <copyright file="NT_NodeSelectionToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    #region Using Statements

    using System.Linq;
    using Colossal.Mathematics;
    using Game.Common;
    using Game.Input;
    using Game.Net;
    using Game.Notifications;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Tools;
    using NetworkTools.Settings;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    #endregion

    #endregion

    public partial class NT_NodeSelectionToolSystem : NT_BaseToolSystem {
        private       ControlPoint             m_ControlPoint;
        private       NativeReference<Entity>  m_LastHoveredEntity;
        private       NativeReference<Entity>  m_LastRaycastEntity;
        private       EntityQuery              m_DefinitionQuery;
        private       float3                   m_LastHitPosition;
        private       IProxyAction             m_ApplyAction;
        private       IProxyAction             m_SecondaryApplyAction;
        private       NativeList<Entity>       m_SelectedNodes;
        private       PrefabBase               m_Prefab;
        private const uint                     MaxNodes            = 2;
        private const float                    MaxDistanceToSelect = 16f;

        public override bool TrySetPrefab(PrefabBase prefab) {
            m_Log.Debug($"TrySetPrefab {prefab is NT_ToolPrefab} {m_PrefabSystem.HasComponent<NT_Select>(prefab)}");
            var validRequest = prefab is NT_ToolPrefab && m_PrefabSystem.HasComponent<NT_Select>(prefab);

            if (!validRequest) {
                return false;
            }

            m_Prefab = prefab;
            return true;
        }

        public override PrefabBase GetPrefab() { return m_Prefab; }

        /// <summary>
        /// Gets the array of currently selected node entities.
        /// </summary>
        /// <returns>Array of selected Entity objects.</returns>
        public Entity[] GetSelectedNodes() {
            return m_SelectedNodes.ToArray(Allocator.Temp).ToArray();
        }

        protected override void OnCreate() {
            ShowNodes              = true;
            m_ApplyAction          = NetworkToolsMod.Instance.Settings.GetAction(NetworkToolsModSettings.ApplyActionName);
            m_SecondaryApplyAction = NetworkToolsMod.Instance.Settings.GetAction(NetworkToolsModSettings.SecondaryApplyActionName);
            m_SelectedNodes        = new NativeList<Entity>(4, Allocator.Persistent);
            m_LastHoveredEntity    = new NativeReference<Entity>(Allocator.Persistent);
            m_LastRaycastEntity    = new NativeReference<Entity>(Allocator.Persistent);
            base.OnCreate();
        }

        protected override void OnDestroy() {
            m_SelectedNodes.Dispose();
            m_LastHoveredEntity.Dispose();
            m_LastRaycastEntity.Dispose();

            base.OnDestroy();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            const string logPrefix = "OnUpdate()";
            UpdateActions();

            // Right click => Remove last point from stack
            if (m_SecondaryApplyAction.WasPressedThisFrame()) {
                RemoveLastPoint();
                return inputDeps;
            }

            // Get raycast result
            if (GetRaycastResult(out var controlPoint)) {
                var hitPos = controlPoint.m_HitPosition;

                if (m_LastHoveredEntity.Value != controlPoint.m_OriginalEntity) {
                    SwapHighlitedEntities(m_LastHoveredEntity.Value, controlPoint.m_OriginalEntity);
                }

                m_LastHoveredEntity.Value = controlPoint.m_OriginalEntity;
                m_LastHitPosition         = hitPos;

                if (m_ApplyAction.WasPressedThisFrame()) {
                    AddPoint(controlPoint.m_OriginalEntity);
                }
            } else {
                ChangeHighlighting(m_LastHoveredEntity.Value, ChangeObjectHighlightMode.RemoveHighlight);
                m_LastHoveredEntity.Value = Entity.Null;
                m_LastHitPosition         = float3.zero;
            }

            return inputDeps;
        }

        private void UpdateActions() {
            m_ApplyAction.shouldBeEnabled = true;
            m_SecondaryApplyAction.shouldBeEnabled = true;
        }

        protected override void OnStartRunning() {
            m_LastHitPosition = default;

            m_ApplyAction.shouldBeEnabled          = true;
            m_SecondaryApplyAction.shouldBeEnabled = true;
        }

        protected override void OnStopRunning() {
            m_ApplyAction.shouldBeEnabled          = false;
            m_SecondaryApplyAction.shouldBeEnabled = false;
        }

        private void AddPoint(Entity entity) {
            if (entity == Entity.Null ||
                m_SelectedNodes.Length >= MaxNodes || 
                m_SelectedNodes.Contains(entity)) {
                return;
            }

            EntityManager.AddComponent<NT_Selected>(entity);
            m_SelectedNodes.Add(entity);
        }

        private void RemoveLastPoint() {
            if (m_SelectedNodes.Length == 0) {
                return;
            }

            RemovePoint(m_SelectedNodes[^1]);
        }

        private void RemovePoint(Entity entity) {
            if (m_SelectedNodes.Length == 0) {
                return;
            }

            EntityManager.RemoveComponent<NT_Selected>(entity);
            var index = m_SelectedNodes.IndexOf(entity);
            if (index == -1) {
                return;
            }
            m_SelectedNodes.RemoveAt(index);
        }

        protected override bool GetRaycastResult(out ControlPoint controlPoint) {
            if (base.GetRaycastResult(out var entity, out RaycastHit raycastHit)) {
                controlPoint = FilterRaycastResult(entity, raycastHit);
                return controlPoint.m_OriginalEntity != Entity.Null;
            }

            controlPoint = default;
            return false;
        }

        private ControlPoint FilterRaycastResult(Entity entity, RaycastHit hit) {
            if (EntityManager.HasComponent<Edge>(entity)) {
                // todo make job
                // Find the closest node to the hit position
                var edge            = EntityManager.GetComponentData<Edge>(entity);
                var startNode       = EntityManager.GetComponentData<Node>(edge.m_Start);
                var distanceToStart = math.distance(hit.m_Position, startNode.m_Position);
                var endNode         = EntityManager.GetComponentData<Node>(edge.m_End);
                var distanceToEnd   = math.distance(hit.m_Position, endNode.m_Position);

                if (distanceToStart < MaxDistanceToSelect && distanceToStart < distanceToEnd) {
                    return new ControlPoint(edge.m_Start, hit);
                } 
                if (distanceToEnd < MaxDistanceToSelect && distanceToEnd < distanceToStart) {
                    return new ControlPoint(edge.m_End, hit);
                }

                return default;
            }

            return new ControlPoint(entity, hit);
        }

        private void SwapHighlitedEntities(Entity oldEntity, Entity newEntity) {
            ChangeHighlighting(oldEntity, ChangeObjectHighlightMode.RemoveHighlight);
            ChangeHighlighting(newEntity, ChangeObjectHighlightMode.AddHighlight);
        }

        private void ChangeHighlighting(Entity entity, ChangeObjectHighlightMode mode) {
            if (entity == Entity.Null || !EntityManager.Exists(entity)) {
                return;
            }

            var wasChanged = false;

            if (mode == ChangeObjectHighlightMode.AddHighlight && !EntityManager.HasComponent<NT_Highlighted>(entity)) {
                EntityManager.AddComponent<NT_Highlighted>(entity);
                wasChanged = true;
            } else if (mode == ChangeObjectHighlightMode.RemoveHighlight && EntityManager.HasComponent<NT_Highlighted>(entity)) {
                EntityManager.RemoveComponent<NT_Highlighted>(entity);
                wasChanged = true;
            }

            if (wasChanged && !EntityManager.HasComponent<BatchesUpdated>(entity)) {
                EntityManager.AddComponent<BatchesUpdated>(entity);
            }
        }

        public override void InitializeRaycast() {
            base.InitializeRaycast();

            m_ToolRaycastSystem.collisionMask   = CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.Underground;
            m_ToolRaycastSystem.typeMask        = TypeMask.Net;
            m_ToolRaycastSystem.netLayerMask    = Layer.All;
            m_ToolRaycastSystem.iconLayerMask   = IconLayerMask.None;
            m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;
            m_ToolRaycastSystem.raycastFlags = RaycastFlags.Markers | RaycastFlags.ElevateOffset | RaycastFlags.SubElements |
                                               RaycastFlags.Cargo   | RaycastFlags.Passenger;
        }

        public override void GetAvailableSnapMask(out Snap onMask, out Snap offMask) {
            //if (this.m_Prefab != null) {
            //    GetCustomAvailableSnapMask(out onMask, out offMask);
            //    return;
            //}
            base.GetAvailableSnapMask(out onMask, out offMask);
        }

        //private static void GetCustomAvailableSnapMask(out Snap onMask, out Snap offMask) {
        //    onMask = Snap.ExistingGeometry | Snap.CellLength | Snap.StraightDirection | Snap.ObjectSide | Snap.GuideLines | Snap.ZoneGrid | Snap.ContourLines
        // | Snap.ObjectSurface | Snap.LotGrid | Snap.AutoParent;
        //    offMask = onMask;
        //}

        internal enum ChangeObjectHighlightMode {
            AddHighlight,
            RemoveHighlight,
        }
    }
}