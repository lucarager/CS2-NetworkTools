namespace NetworkTools.Systems.Tools.RoadShape {
    using Game.Net;
    using NetworkTools.Components;
    using Unity.Entities;
    using Unity.Mathematics;

    public partial class NT_RoadShapeToolSystem {
        /// <summary>
        ///     Creates handles for active transform.
        /// </summary>
        private void CreateTransformHandles() {
            DestroyAllHandles();

            var pathStartPos = EntityManager.GetComponentData<Node>(m_SelectedNodes[0]).m_Position;
            var pathEndPos = EntityManager.GetComponentData<Node>(m_SelectedNodes[^1]).m_Position;

            // Create handles for transform config...
            switch (ShapeTransformConfig.Template)
            {
                case ShapeTransformTemplate.SlopeEaseInOut:
                    var easeInPos = math.lerp(pathStartPos, pathEndPos, ShapeTransformConfig.EaseInControlPoint);
                    var easeOutPos = math.lerp(pathEndPos,  pathStartPos, ShapeTransformConfig.EaseOutControlPoint);

                    CreateParameterHandle(
                        Entity.Null,
                        TransformHandleKeys.EaseInLength,
                        easeInPos + new float3(0, 3, 0),
                        ShapeTransformConfig.EaseInControlPoint,
                        0f, 0.5f,
                        HandleTypeFlags.SlopeControl | HandleTypeFlags.Parameter);

                    CreateParameterHandle(
                        Entity.Null,
                        TransformHandleKeys.EaseOutLength,
                        easeOutPos + new float3(0, 3, 0),
                        ShapeTransformConfig.EaseOutControlPoint,
                        0f, 0.5f,
                        HandleTypeFlags.SlopeControl | HandleTypeFlags.Parameter);
                    break;
            }
        }


        protected override void OnHandleDragging(Entity handle) {
            var link = EntityManager.GetComponentData<NT_HandleLink>(handle);

            switch (link.Key)
            {
                case TransformHandleKeys.EaseInLength:
                    var easeInValue = EntityManager.GetComponentData<NT_HandleValue>(handle);
                    ShapeTransformConfig.EaseInControlPoint = easeInValue.Value;
                    break;

                case TransformHandleKeys.EaseOutLength:
                    var easeOutValue = EntityManager.GetComponentData<NT_HandleValue>(handle);
                    ShapeTransformConfig.EaseOutControlPoint = easeOutValue.Value;
                    break;
            }

            m_UpdateNeeded = true;
        }
    }
}