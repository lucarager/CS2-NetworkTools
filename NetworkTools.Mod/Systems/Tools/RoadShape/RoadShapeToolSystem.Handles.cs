namespace NetworkTools.Systems.Tools.RoadShape {
    using Game.Net;
    using NetworkTools.Components;
    using Unity.Entities;
    using Unity.Mathematics;

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
                case TransformHandleKeys.EaseInLength:
                    var easeInParam = CalculateEaseParameter(
                        handlePos,
                        m_ShapeTransformContext.StartPosition,
                        m_ShapeTransformContext.EndPosition);
                    m_Log.Debug($"EaseInLength: {ShapeTransformConfig.EaseInLength} -> {easeInParam}");
                    ShapeTransformConfig.EaseInLength = easeInParam;
                    break;

                case TransformHandleKeys.EaseOutLength:
                    var easeOutParam = CalculateEaseParameter(
                        handlePos,
                        m_ShapeTransformContext.EndPosition,
                        m_ShapeTransformContext.StartPosition);
                    m_Log.Debug($"EaseOutLength: {ShapeTransformConfig.EaseOutLength} -> {easeOutParam}");
                    ShapeTransformConfig.EaseOutLength = easeOutParam;
                    break;
            }

            m_UpdateNeeded = true;
        }

        /// <summary>
        /// Calculates the ease parameter (0-0.5) from handle world position.
        /// Projects handle onto path and returns normalized distance from origin.
        /// </summary>
        private float CalculateEaseParameter(float3 handlePos, float3 pathOrigin, float3 pathEnd) {
            // Use XZ plane for consistent calculations (ignore elevation differences)
            var pathVectorXZ = new float2(pathEnd.x - pathOrigin.x, pathEnd.z - pathOrigin.z);
            var pathLengthXZ = math.length(pathVectorXZ);

            if (pathLengthXZ < 0.001f) {
                m_Log.Debug("CalculateEaseParameter: path too short");
                return 0f;
            }

            var pathDirectionXZ = pathVectorXZ / pathLengthXZ;

            // Project handle position onto the path line in XZ plane
            var handleOffsetXZ = new float2(handlePos.x - pathOrigin.x, handlePos.z - pathOrigin.z);
            var projectedDistance = math.dot(handleOffsetXZ, pathDirectionXZ);

            // Normalize to 0-1 range, then clamp to 0-0.5
            var normalizedParam = projectedDistance / pathLengthXZ;
            var result = math.clamp(normalizedParam, 0f, 0.5f);

            m_Log.Debug($"CalculateEaseParameter: projDist={projectedDistance:F2}, pathLen={pathLengthXZ:F2}, result={result:F3}");
            return result;
        }
    }
}