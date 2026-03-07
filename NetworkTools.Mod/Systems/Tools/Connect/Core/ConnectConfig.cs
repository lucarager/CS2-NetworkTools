namespace NetworkTools.Systems.Tools.Connect {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Colossal.UI.Binding;
    using Unity.Mathematics;

    /// <summary>
    /// Struct to hold connection parameters and their values.
    /// </summary>
    public struct ConnectConfig : IJsonWritable, IJsonReadable {
        // Shared
        public float3 StartPosition;
        public float3 EndPosition;
        public float3 StartDirection;
        public float3 EndDirection;

        // Curve
        public float3 CurveStartPointPosition;
        public float3 CurveStartControlPointPosition;
        public float3 CurveEndControlPointPosition;
        public float3 CurveEndPointPosition;

        // Loop
        public float3 LoopControlPointPosition;
        public float Radius;

        public ConnectConfig(float3 startPosition, float3 endPosition, float3 startDirection, float3 endDirection) {
            StartPosition = startPosition;
            EndPosition = endPosition;
            StartDirection = startDirection;
            EndDirection = endDirection;
            CurveStartPointPosition = default;
            CurveStartControlPointPosition = default;
            CurveEndControlPointPosition = default;
            CurveEndPointPosition = default;
            LoopControlPointPosition = default;
            Radius = default;
        }

        /// <inheritdoc/>
        public void Write(IJsonWriter writer) {
            writer.TypeBegin(GetType().FullName);
            writer.TypeEnd();
        }

        /// <inheritdoc/>
        public void Read(IJsonReader reader) {
            reader.ReadMapBegin();
            reader.ReadMapEnd();
        }
    }
}
