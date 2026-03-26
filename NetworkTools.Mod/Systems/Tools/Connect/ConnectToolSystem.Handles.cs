namespace NetworkTools.Systems.Tools.Connect {
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
        /// </summary>
        private void RefreshTransformHandles() {
            DestroyAllHandles();

            m_Log.Debug($"RefreshTransformHandles: Creating handles");

            var handleDefs = GetHandleDefinitions();
            CreateHandlesFromDefinitions(handleDefs);
        }

        /// <summary>
        /// Gets handle definitions for the current mode.
        /// </summary>
        private TransformHandleDefinition[] GetHandleDefinitions() {
            switch (CurrentMode)
            {
                case ConnectMode.SimpleCurve:
                    return new SimpleCurveGenerator().GetHandleDefinitions(CurrentMode, CurrentConfig);
                case ConnectMode.Loop:
                    return new LoopGenerator().GetHandleDefinitions(CurrentMode, CurrentConfig);
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

            // Propagate delta to child handles and update their configs
            if (math.lengthsq(delta) > 0f) {
                PropagateToChildren(handle, delta);
                ApplyChildConfigs(handle);
            }

            m_UpdateNeeded = true;
        }

        /// <summary>
        /// Syncs config positions for all children of the given parent handle.
        /// </summary>
        private void ApplyChildConfigs(Entity parentHandle) {
            for (var i = 0; i < m_Handles.Length; i++) {
                var child = m_Handles[i];
                if (!EntityManager.HasComponent<NT_HandleParent>(child)) {
                    continue;
                }

                var parentComponent = EntityManager.GetComponentData<NT_HandleParent>(child);
                if (parentComponent.Parent != parentHandle) {
                    continue;
                }

                var childLink = EntityManager.GetComponentData<NT_HandleLink>(child);
                var childPos = EntityManager.GetComponentData<NT_HandlePosition>(child).Position;
                ApplyConfigPosition(childLink.Key, childPos);
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
                HandleKeys.LoopControlPointPosition       => CurrentConfig.LoopControlPointPosition,
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
