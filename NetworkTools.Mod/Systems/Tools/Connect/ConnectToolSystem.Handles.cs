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
        /// Dispatches to the appropriate handler based on handle type.
        /// </summary>
        protected override void OnHandleDragging(Entity handle) {
            var link = EntityManager.GetComponentData<NT_HandleLink>(handle);
            var handlePos = EntityManager.GetComponentData<NT_HandlePosition>(handle).Position;

            m_Log.Debug($"OnHandleDragging: key={link.Key}, handlePos={handlePos}");

            if (EntityManager.HasComponent<NT_HandleCircle>(handle)) {
                HandleCircleDrag(handle);
            } else {
                HandlePositionDrag(handle, link.Key, handlePos);
            }

            m_UpdateNeeded = true;
        }

        /// <summary>
        /// Handles dragging for circle handles.
        /// Delegates to the base class for ECS updates and writes the computed radius to the config.
        /// </summary>
        private void HandleCircleDrag(Entity handle) {
            var center = CurrentConfig.LoopControlPointPosition;
            CurrentConfig.LoopRadius = UpdateCircleHandleRadius(handle, center);
        }

        /// <inheritdoc />
        protected override float3 GetHandleConfigPosition(int key) {
            return key switch {
                HandleKeys.CurveStartPointPosition        => CurrentConfig.CurveStartPointPosition,
                HandleKeys.CurveStartControlPointPosition => CurrentConfig.CurveStartControlPointPosition,
                HandleKeys.CurveEndControlPointPosition   => CurrentConfig.CurveEndControlPointPosition,
                HandleKeys.CurveEndPointPosition          => CurrentConfig.CurveEndPointPosition,
                HandleKeys.LoopControlPointPosition       => CurrentConfig.LoopControlPointPosition,
                _                                         => float3.zero
            };
        }

        /// <inheritdoc />
        protected override void ApplyHandleConfigPosition(int key, float3 position) {
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
                case HandleKeys.LoopControlPointPosition:
                    CurrentConfig.LoopControlPointPosition = position;
                    break;
            }
        }
    }
}
