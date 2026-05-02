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
            var jobConfig = BuildJobConfig();

            switch (Mode.Value) {
                case ConnectMode.SimpleCurve:
                    return new SimpleCurveGenerator().GetHandleDefinitions(jobConfig);
                case ConnectMode.Loop:
                    return new LoopGenerator().GetHandleDefinitions(jobConfig);
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
            LoopRadius.Value = radius;
        }

        /// <inheritdoc />
        protected override void OnRotationHandleDragged(Entity handle, int key, float angle, float3 direction) {
            m_Log.Debug($"OnRotationHandleDragged: key={key}, angle={angle}");
            switch (key) {
                case HandleKeys.StartDirection:
                    StartDirection.Value = direction;
                    break;
                case HandleKeys.EndDirection:
                    EndDirection.Value = direction;
                    break;
            }
        }

        /// <inheritdoc />
        protected override float3 GetHandleConfigPosition(int key) {
            return key switch {
                HandleKeys.CurveStartPointPosition        => CurveStartPointPosition.Value,
                HandleKeys.CurveStartControlPointPosition => CurveStartControlPointPosition.Value,
                HandleKeys.CurveEndControlPointPosition   => CurveEndControlPointPosition.Value,
                HandleKeys.CurveEndPointPosition          => CurveEndPointPosition.Value,
                HandleKeys.LoopControlPointPosition       => LoopControlPointPosition.Value,
                _                                         => float3.zero
            };
        }

        /// <inheritdoc />
        protected override void ApplyHandleConfigPosition(int key, float3 position) {
            switch (key) {
                case HandleKeys.CurveStartPointPosition:
                    CurveStartPointPosition.Value = position;
                    break;
                case HandleKeys.CurveStartControlPointPosition:
                    CurveStartControlPointPosition.Value = position;
                    break;
                case HandleKeys.CurveEndControlPointPosition:
                    CurveEndControlPointPosition.Value = position;
                    break;
                case HandleKeys.CurveEndPointPosition:
                    CurveEndPointPosition.Value = position;
                    break;
                case HandleKeys.LoopControlPointPosition:
                    LoopControlPointPosition.Value = position;
                    break;
            }
        }
    }
}
