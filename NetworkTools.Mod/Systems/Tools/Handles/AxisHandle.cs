namespace NetworkTools.Systems.Tools.Handles {
    using System;
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Parameters;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Handle spec for a scalar parameter that maps to a position along a line
    ///     between two dynamic endpoints. 
    /// </summary>
    public class AxisHandle : IHandleSpec<float> {
        /// <summary>Dynamic start point of the axis. Evaluated at handle creation and during drag.</summary>
        public Func<NT_BaseToolSystem, float3> StartPoint { get; init; }

        /// <summary>Dynamic end point of the axis. Evaluated at handle creation and during drag.</summary>
        public Func<NT_BaseToolSystem, float3> EndPoint { get; init; }

        /// <summary>Vertical offset applied to the handle position for visibility above the surface.</summary>
        public float YOffset { get; init; }

        /// <summary>
        ///     When false (default), value 0 maps to <see cref="StartPoint"/> and value 1 maps to <see cref="EndPoint"/>.
        ///     When true, value 0 maps to <see cref="EndPoint"/> and value 1 maps to <see cref="StartPoint"/>.
        /// </summary>
        public bool Reverse { get; init; }

        public Dependency[]          DependsOn          { get; init; }
        public string                RenderConnectionTo { get; init; }
        public NT_HandleConstraints? Constraints => null; // Computed dynamically in CreateHandleFromSpec
        public float                 Size      { get; init; } = NT_Handle.SizePrimary;
        public HandleSnap            Snap      { get; init; } = HandleSnap.None;

        HandleTypeFlags IHandleSpec.TypeFlags => HandleTypeFlags.Position | HandleTypeFlags.AxisHandle;

        private ComputePositionDelegate<float>     m_ComputePosition;
        private ComputeFromPositionDelegate<float> m_ComputeFromPosition;

        ComputePositionDelegate<float> IHandleSpec<float>.ComputePosition =>
            m_ComputePosition ??= ComputeWorldPosition;

        ComputeFromPositionDelegate<float> IHandleSpec<float>.ComputeFromPosition =>
            m_ComputeFromPosition ??= ComputeValueFromPosition;

        public void SyncToEntity(NT_BaseToolSystem tool, Entity entity, ParameterBase param) {
            var fp  = (FloatParameter)param;
            var pos = ComputeWorldPosition(tool, fp.Value);
            tool.EntityManager.SetComponentData(entity, new NT_HandlePosition { Position = pos });

            GetAxisInfo(tool, out var origin, out var axisDir, out var pathLength);
            var constraints = NT_HandleConstraints.AxisWithBounds(axisDir, origin, fp.Min * pathLength, fp.Max * pathLength);
            if (tool.EntityManager.HasComponent<NT_HandleConstraints>(entity)) {
                tool.EntityManager.SetComponentData(entity, constraints);
            } else {
                tool.EntityManager.AddComponentData(entity, constraints);
            }
        }

        /// <summary>
        ///     Re-resolve: re-derive position and axis constraint from the (multi-input) endpoint
        ///     delegates. Accepts any number of sources — listing them all in <c>DependsOn</c> makes
        ///     the handle re-project its same value onto the new axis when any input changes.
        /// </summary>
        public void OnDependencyChanged(NT_BaseToolSystem tool, Entity entity,
                                        ParameterBase owner, Float3Parameter source, float3 delta) {
            SyncToEntity(tool, entity, owner);
        }

        /// <summary>
        ///     Returns the axis origin and direction for the current endpoints.
        ///     Used by <see cref="NT_BaseToolSystem.CreateHandleFromSpec"/> to compute constraints.
        /// </summary>
        internal void GetAxisInfo(NT_BaseToolSystem tool, out float3 origin, out float3 direction, out float pathLength) {
            var from = Reverse ? EndPoint(tool) : StartPoint(tool);
            var to   = Reverse ? StartPoint(tool) : EndPoint(tool);

            origin     = new float3(from.x, from.y + YOffset, from.z);
            direction  = math.normalizesafe(new float3(to.x - from.x, 0f, to.z - from.z));
            pathLength = math.distance(from.xz, to.xz);
        }

        private float3 ComputeWorldPosition(NT_BaseToolSystem tool, float value) {
            var from = Reverse ? EndPoint(tool) : StartPoint(tool);
            var to   = Reverse ? StartPoint(tool) : EndPoint(tool);
            var pos  = math.lerp(from, to, value);
            pos.y    = from.y + YOffset;
            return pos;
        }

        private float ComputeValueFromPosition(NT_BaseToolSystem tool, float3 pos) {
            var from = Reverse ? EndPoint(tool) : StartPoint(tool);
            var to   = Reverse ? StartPoint(tool) : EndPoint(tool);
            var path = to.xz - from.xz;
            var len  = math.length(path);
            if (len < 0.001f) return 0f;
            var axis   = path / len;
            var offset = pos.xz - from.xz;
            return math.dot(offset, axis) / len;
        }
    }
}
