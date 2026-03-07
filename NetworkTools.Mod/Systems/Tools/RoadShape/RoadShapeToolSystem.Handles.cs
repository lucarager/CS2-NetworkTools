namespace NetworkTools.Systems.Tools.RoadShape {
    using Game.Common;
    using Game.Notifications;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;

    using NetworkTools.Components.Handles;
    using NetworkTools.Components;
    using NetworkTools.Components.Tools;

    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Jobs;
    using Unity.Collections;

    public partial class NT_RoadShapeToolSystem {
        /// <summary>
        /// Called each frame while dragging a handle.
        /// Converts handle world position to parameter value (0-0.5) for ease handles.
        /// </summary>
        protected override void OnHandleDragging(Entity handle) {
            var link = EntityManager.GetComponentData<NT_HandleLink>(handle);
            var handlePos = EntityManager.GetComponentData<NT_HandlePosition>(handle).Position;

            m_Log.Debug($"OnHandleDragging: key={link.Key}, handlePos={handlePos}");

            switch (link.Key)
            {
                case HandleKeys.EaseInLength:
                    var easeInAxis = GetHandleConstraintAxisXZ(handle, m_ShapeTransformContext.StartPosition, m_ShapeTransformContext.EndPosition);
                    var easeInParam = CalculateEaseParameter(
                        handlePos,
                        m_ShapeTransformContext.StartPosition,
                        m_ShapeTransformContext.EndPosition,
                        easeInAxis);
                    m_Log.Debug($"EaseInLength: {ShapeTransformConfig.EaseInLength} -> {easeInParam}");
                    ShapeTransformConfig.EaseInLength = easeInParam;
                    break;

                case HandleKeys.EaseOutLength:
                    var easeOutAxis = GetHandleConstraintAxisXZ(handle, m_ShapeTransformContext.EndPosition, m_ShapeTransformContext.StartPosition);
                    var easeOutParam = CalculateEaseParameter(
                        handlePos,
                        m_ShapeTransformContext.EndPosition,
                        m_ShapeTransformContext.StartPosition,
                        easeOutAxis);
                    m_Log.Debug($"EaseOutLength: {ShapeTransformConfig.EaseOutLength} -> {easeOutParam}");
                    ShapeTransformConfig.EaseOutLength = easeOutParam;
                    break;
            }

            m_UpdateNeeded = true;
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
        /// Calculates the ease parameter (0-0.5) from handle world position.
        /// Projects handle onto the handle's constraint axis and returns the normalized distance from origin.
        /// </summary>
        private float CalculateEaseParameter(float3 handlePos, float3 pathOrigin, float3 pathEnd, float2 axisDirectionXZ) {
            // Use XZ plane for consistent calculations (ignore elevation differences)
            var pathVectorXZ = new float2(pathEnd.x - pathOrigin.x, pathEnd.z - pathOrigin.z);
            var pathLengthXZ = math.length(pathVectorXZ);

            if (pathLengthXZ < 0.001f) {
                m_Log.Debug("CalculateEaseParameter: path too short");
                return 0f;
            }

            // Project handle position onto the actual constraint axis (not the straight start→end direction)
            var handleOffsetXZ = new float2(handlePos.x - pathOrigin.x, handlePos.z - pathOrigin.z);
            var projectedDistance = math.dot(handleOffsetXZ, axisDirectionXZ);

            // Normalize to 0-1 range, then clamp to 0-0.5
            var normalizedParam = projectedDistance / pathLengthXZ;
            var result = math.clamp(normalizedParam, 0f, 0.5f);

            m_Log.Debug($"CalculateEaseParameter: projDist={projectedDistance:F2}, pathLen={pathLengthXZ:F2}, result={result:F3}");
            return result;
        }
    }
}