namespace NetworkTools.Systems.Tools.RoadShape {
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Parameters;

    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Unity.Collections;

    public partial class NT_RoadShapeToolSystem {
        /// <inheritdoc />
        protected override void OnParameterHandleDragged(Entity handle, float3 position, float value) {
            m_Log.Debug($"OnParameterHandleDragged: position={position}, value={value}");

            if (!m_HandleParameterMap.TryGetValue(handle, out var param)) {
                return;
            }

            if (param == EaseInLength) {
                var axis = GetHandleConstraintAxisXZ(handle, m_ShapeTransformContext.StartPosition, m_ShapeTransformContext.EndPosition);
                var easeParam = CalculateEaseParameter(
                    position,
                    m_ShapeTransformContext.StartPosition,
                    m_ShapeTransformContext.EndPosition,
                    axis,
                    EaseInLength.Min,
                    EaseInLength.Max);
                m_Log.Debug($"EaseInLength: {EaseInLength.Value} -> {easeParam}");
                EaseInLength.Value = easeParam;
            } else if (param == EaseOutLength) {
                var axis = GetHandleConstraintAxisXZ(handle, m_ShapeTransformContext.EndPosition, m_ShapeTransformContext.StartPosition);
                var easeParam = CalculateEaseParameter(
                    position,
                    m_ShapeTransformContext.EndPosition,
                    m_ShapeTransformContext.StartPosition,
                    axis,
                    EaseOutLength.Min,
                    EaseOutLength.Max);
                m_Log.Debug($"EaseOutLength: {EaseOutLength.Value} -> {easeParam}");
                EaseOutLength.Value = easeParam;
            }
        }

        /// <summary>
        /// Returns the XZ constraint axis for a handle, falling back to the straight pathOrigin→pathEnd
        /// direction if no axis constraint is present.
        /// </summary>
        private float2 GetHandleConstraintAxisXZ(Entity handle, float3 pathOrigin, float3 pathEnd) {
            if (EntityManager.HasComponent<NT_HandleConstraints>(handle)) {
                var snapAxis = EntityManager.GetComponentData<NT_HandleConstraints>(handle).SnapAxis.xz;
                if (math.lengthsq(snapAxis) > 0.0001f) {
                    return math.normalize(snapAxis);
                }
            }
            var fallback = new float2(pathEnd.x - pathOrigin.x, pathEnd.z - pathOrigin.z);
            var len = math.length(fallback);
            return len > 0.001f ? fallback / len : float2.zero;
        }

        /// <summary>
        /// Calculates the normalized ease parameter from a handle world position.
        /// Projects the handle onto its constraint axis and clamps to the parameter's declared range.
        /// </summary>
        private float CalculateEaseParameter(float3 handlePos, float3 pathOrigin, float3 pathEnd, float2 axisDirectionXZ, float min, float max) {
            var pathVectorXZ = new float2(pathEnd.x - pathOrigin.x, pathEnd.z - pathOrigin.z);
            var pathLengthXZ = math.length(pathVectorXZ);

            if (pathLengthXZ < 0.001f) {
                m_Log.Debug("CalculateEaseParameter: path too short");
                return min;
            }

            var handleOffsetXZ = new float2(handlePos.x - pathOrigin.x, handlePos.z - pathOrigin.z);
            var projectedDistance = math.dot(handleOffsetXZ, axisDirectionXZ);

            var normalizedParam = projectedDistance / pathLengthXZ;
            var result = math.clamp(normalizedParam, min, max);

            m_Log.Debug($"CalculateEaseParameter: projDist={projectedDistance:F2}, pathLen={pathLengthXZ:F2}, result={result:F3}");
            return result;
        }
    }
}
