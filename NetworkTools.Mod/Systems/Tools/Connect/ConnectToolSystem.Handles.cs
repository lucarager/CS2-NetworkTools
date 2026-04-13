namespace NetworkTools.Systems.Tools.Connect {
    using NetworkTools.Systems.Tools.Base;
    using Unity.Entities;
    using Unity.Mathematics;

    public partial class NT_ConnectToolSystem {
        /// <summary>
        ///     Creates or refreshes handles based on the current mode and config.
        /// </summary>
        private void RefreshTransformHandles() {
            DestroyAllHandles();

            m_Log.Debug("RefreshTransformHandles: Creating handles");

            var handleDefs = GetHandleDefinitions();
            CreateHandlesFromDefinitions(handleDefs);
        }

        /// <summary>
        ///     Gets handle definitions for the current mode.
        /// </summary>
        private TransformHandleDefinition[] GetHandleDefinitions() {
            switch (CurrentMode) {
                case ConnectMode.SimpleCurve:
                    return new SimpleCurveGenerator().GetHandleDefinitions(CurrentMode, CurrentConfig);
                case ConnectMode.Loop:
                    return new LoopGenerator().GetHandleDefinitions(CurrentMode, CurrentConfig);
                default:
                    return System.Array.Empty<TransformHandleDefinition>();
            }
        }

        /// <inheritdoc />
        protected override void OnPositionHandleDragged(Entity handle, int key, float3 position) {
            m_Log.Debug($"OnPositionHandleDragged: key={key}, position={position}");
            HandlePositionDrag(handle, key, position);
        }

        /// <inheritdoc />
        protected override void OnCircleHandleDragged(Entity handle, int key, float radius) {
            m_Log.Debug($"OnCircleHandleDragged: key={key}, radius={radius}");
            CurrentConfig.LoopRadius = radius;
        }

        /// <inheritdoc />
        protected override void OnRotationHandleDragged(Entity handle, int key, float angle, float3 direction) {
            m_Log.Debug($"OnRotationHandleDragged: key={key}, angle={angle}");
            switch (key) {
                case HandleKeys.StartDirection:
                    CurrentConfig.StartDirection = direction;
                    break;
                case HandleKeys.EndDirection:
                    CurrentConfig.EndDirection = direction;
                    break;
            }
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