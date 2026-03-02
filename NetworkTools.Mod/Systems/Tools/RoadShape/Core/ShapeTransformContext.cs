namespace NetworkTools.Systems.Tools.RoadShape {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Unity.Mathematics;

    /// <summary>
    /// An enum for network composition.
    /// </summary>
    public enum NetworkComposition {
        None,
        Ground = 1,
        Elevated = 2,
        Tunnel = 4,
    }

    /// <summary>
    /// Path-level context data for the transformation pipeline.
    /// Immutable after initialization - contains input configuration and derived values.
    /// </summary>
    public struct ShapeTransformContext {
        /// <summary>
        /// Full 3D position of the path start node.
        /// </summary>
        public float3 StartPosition;

        /// <summary>
        /// Full 3D position of the path end node.
        /// </summary>
        public float3 EndPosition;

        /// <summary>
        /// Total length of all edges in the path.
        /// </summary>
        public float TotalLength;

        /// <summary>
        /// Height (Y) at path start. Convenience accessor for StartPosition.y.
        /// </summary>
        public float StartHeight => StartPosition.y;

        /// <summary>
        /// Height difference from start to end (EndPosition.y - StartPosition.y).
        /// </summary>
        public float DeltaHeight => EndPosition.y - StartPosition.y;

        /// <summary>
        /// XZ position at path start.
        /// </summary>
        public float2 StartXZ => new float2(StartPosition.x, StartPosition.z);

        /// <summary>
        /// XZ position at path end.
        /// </summary>
        public float2 EndXZ => new float2(EndPosition.x, EndPosition.z);

        /// <summary>
        /// Whether this context has valid data for processing.
        /// </summary>
        public bool IsValid => TotalLength > 0f;

        /// <summary>
        /// Creates a new ShapeTransformContext from path endpoint positions.
        /// </summary>
        public static ShapeTransformContext Create(float3 startPosition, float3 endPosition) {
            return new ShapeTransformContext {
                StartPosition = startPosition,
                EndPosition = endPosition,
                TotalLength = 0f, // Set after gathering edges
            };
        }
    }
}
