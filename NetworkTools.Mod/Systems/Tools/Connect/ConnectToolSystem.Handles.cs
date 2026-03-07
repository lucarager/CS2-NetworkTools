namespace NetworkTools.Systems.Tools.Connect {
    using System.Collections.Generic;

    using Game.Net;
    using Game.Prefabs;

    using NetworkTools.Components;
    using NetworkTools.Components.Handles;
    using NetworkTools.Components.Tools;
    using NetworkTools.Systems.Tools.Base;
    using NetworkTools.Systems.Tools.RoadShape;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Jobs;

    public partial class NT_ConnectToolSystem {
        /// <summary>
        /// Creates or refreshes handles based on the current mode and config.
        /// Two-pass: creates root handles first, then children with NT_HandleParent resolved.
        /// </summary>
        private void RefreshTransformHandles() {
            DestroyAllHandles();

            m_Log.Debug($"RefreshTransformHandles: Creating handles");

            var handleDefs = GetHandleDefinitions();

            // Key → Entity mapping for resolving parent references
            var keyToEntity = new Dictionary<int, Entity>(handleDefs.Length);

            // Pass 1: create root handles (no parent)
            foreach (var def in handleDefs) {
                if (def.ParentKey != TransformHandleDefinition.NoParent) {
                    continue;
                }

                var entity = CreateHandleFromDefinition(def);
                keyToEntity[def.Key] = entity;
            }

            // Pass 2: create child handles and attach NT_HandleParent
            foreach (var def in handleDefs) {
                if (def.ParentKey == TransformHandleDefinition.NoParent) {
                    continue;
                }

                var entity = CreateHandleFromDefinition(def);
                keyToEntity[def.Key] = entity;

                if (keyToEntity.TryGetValue(def.ParentKey, out var parentEntity)) {
                    EntityManager.AddComponentData(entity, new NT_HandleParent { Parent = parentEntity });
                }
            }
        }

        /// <summary>
        /// Creates a handle entity from a definition, dispatching by type flags.
        /// </summary>
        private Entity CreateHandleFromDefinition(TransformHandleDefinition def) {
            var radius = def.Radius > 0f ? def.Radius : NT_Handle.PrimaryRadius;

            if ((def.TypeFlags & HandleTypeFlags.Parameter) != 0) {
                return CreateParameterHandle(
                    Entity.Null,
                    def.Key,
                    def.Position,
                    def.Value,
                    def.MinValue,
                    def.MaxValue,
                    def.TypeFlags,
                    def.Constraints,
                    radius);
            }

            return CreatePositionHandle(
                Entity.Null,
                Entity.Null,
                def.Key,
                def.Position,
                def.TypeFlags,
                def.Constraints,
                radius);
        }

        /// <summary>
        /// Gets handle definitions for the current mode.
        /// </summary>
        private TransformHandleDefinition[] GetHandleDefinitions() {
            switch (CurrentMode)
            {
                case ConnectMode.SimpleCurve:
                    return new SimpleCurveGenerator().GetHandleDefinitions(CurrentMode, CurrentConfig);
                default:
                    return System.Array.Empty<TransformHandleDefinition>();
            }
        }

        /// <summary>
        /// Called each frame while dragging a handle.
        /// Updates the config position fields and propagates movement to child handles.
        /// </summary>
        protected override void OnHandleDragging(Entity handle) {
            var link = EntityManager.GetComponentData<NT_HandleLink>(handle);
            var handlePos = EntityManager.GetComponentData<NT_HandlePosition>(handle).Position;

            m_Log.Debug($"OnHandleDragging: key={link.Key}, handlePos={handlePos}");

            // Compute delta before updating config (need previous position)
            var previousPos = GetConfigPosition(link.Key);
            var delta = handlePos - previousPos;

            // Update config for the dragged handle
            ApplyConfigPosition(link.Key, handlePos);

            // Propagate delta to child handles
            if (math.lengthsq(delta) > 0f) {
                PropagateToChildren(handle, delta);
            }

            m_UpdateNeeded = true;
        }

        /// <summary>
        /// Moves all child handles of the given parent by the specified delta.
        /// </summary>
        private void PropagateToChildren(Entity parentHandle, float3 delta) {
            for (var i = 0; i < m_Handles.Length; i++) {
                var child = m_Handles[i];
                if (!EntityManager.HasComponent<NT_HandleParent>(child)) {
                    continue;
                }

                var parentComponent = EntityManager.GetComponentData<NT_HandleParent>(child);
                if (parentComponent.Parent != parentHandle) {
                    continue;
                }

                // Move child position
                var childPos = EntityManager.GetComponentData<NT_HandlePosition>(child);
                var newChildPos = childPos.Position + delta;
                childPos.Position = newChildPos;
                EntityManager.SetComponentData(child, childPos);

                // Update config for the child
                var childLink = EntityManager.GetComponentData<NT_HandleLink>(child);
                ApplyConfigPosition(childLink.Key, newChildPos);
            }
        }

        /// <summary>
        /// Gets the current config position for a handle key.
        /// </summary>
        private float3 GetConfigPosition(int key) {
            return key switch {
                HandleKeys.CurveStartPointPosition        => CurrentConfig.CurveStartPointPosition,
                HandleKeys.CurveStartControlPointPosition => CurrentConfig.CurveStartControlPointPosition,
                HandleKeys.CurveEndControlPointPosition   => CurrentConfig.CurveEndControlPointPosition,
                HandleKeys.CurveEndPointPosition          => CurrentConfig.CurveEndPointPosition,
                _                                         => float3.zero
            };
        }

        /// <summary>
        /// Writes a position to the config field identified by the handle key.
        /// </summary>
        private void ApplyConfigPosition(int key, float3 position) {
            switch (key) {
                case HandleKeys.CurveStartPointPosition:
                    CurrentConfig.CurveStartPointPosition = position;
                    break;
                case HandleKeys.CurveStartControlPointPosition:
                    CurrentConfig.CurveStartControlPointPosition = position;
                    break;
                case HandleKeys.CurveEndControlPointPosition:
                    CurrentConfig.CurveEndControlPointPosition = position;
                    break;
                case HandleKeys.CurveEndPointPosition:
                    CurrentConfig.CurveEndPointPosition = position;
                    break;
            }
        }
    }
}
