// <copyright file="EdgeTransformState.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools.PathTransform {
    #region Using Statements

    using Colossal.Mathematics;
    using Unity.Entities;
    using Unity.Mathematics;

    #endregion

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
    public struct TransformContext {
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
        /// The transformation configuration.
        /// </summary>
        public TransformConfig Config;

        /// <summary>
        /// Whether this context has valid data for processing.
        /// </summary>
        public bool IsValid => TotalLength > 0f;

        /// <summary>
        /// Creates a new TransformContext from path endpoint positions.
        /// </summary>
        public static TransformContext Create(float3 startPosition, float3 endPosition, TransformConfig config) {
            return new TransformContext {
                StartPosition = startPosition,
                EndPosition   = endPosition,
                TotalLength   = 0f, // Set after gathering edges
                Config        = config,
            };
        }
    }

    /// <summary>
    /// Per-edge state that flows through the transformation pipeline.
    /// Mutable - updated by each transform stage.
    /// </summary>
    public struct EdgeTransformState {
        // === Identity (immutable after creation) ===

        /// <summary>
        /// The edge entity being transformed.
        /// </summary>
        public Entity EdgeEntity;

        /// <summary>
        /// The start node entity of the edge (in edge direction, not path direction).
        /// </summary>
        public Entity StartNode;

        /// <summary>
        /// The end node entity of the edge (in edge direction, not path direction).
        /// </summary>
        public Entity EndNode;

        /// <summary>
        /// Index of this edge in the path (0-based).
        /// </summary>
        public int PathIndex;

        /// <summary>
        /// True if the edge direction matches the path direction.
        /// </summary>
        public bool IsForward;

        /// <summary>
        /// Network composition of the edge.
        /// </summary>
        public NetworkComposition NetworkComposition;

        // === Geometry (mutable - updated by transforms) ===

        /// <summary>
        /// The current bezier curve. Updated by shape and slope transforms.
        /// </summary>
        public Bezier4x3 Bezier;

        /// <summary>
        /// Length of the edge.
        /// </summary>
        public float Length;

        /// <summary>
        /// Ratio (0-1) of the control point closer to path-start.
        /// Updated after shape transforms to reflect new positions.
        /// </summary>
        public float CtrlStartRatio;

        /// <summary>
        /// Ratio (0-1) of the control point closer to path-end.
        /// Updated after shape transforms to reflect new positions.
        /// </summary>
        public float CtrlEndRatio;

        /// <summary>
        /// Cumulative distance along the path at the start of this edge.
        /// </summary>
        public float CumulativeDistance;

        // === Original values (immutable - for intersection delta calculations) ===

        /// <summary>
        /// Original height (Y) at the path-end of this edge.
        /// Used to calculate height delta for intersection adjustments.
        /// </summary>
        public float OriginalEndHeight;

        /// <summary>
        /// Original XZ position at the path-end of this edge.
        /// Used to calculate XZ delta for intersection adjustments.
        /// </summary>
        public float2 OriginalEndXZ;

        /// <summary>
        /// Calculates the edge length from the current bezier geometry.
        /// </summary>
        public void CalculateLength() {
            Length = MathUtils.Length(Bezier);
        }

        /// <summary>
        /// Recalculates the control point ratios based on the current bezier geometry.
        /// Call this after modifying the bezier's XZ positions.
        /// </summary>
        public void RecalculateControlPointRatios() {
            ShapeCalculator.CalculateControlPointRatios(
                Bezier,
                Length,
                IsForward,
                out CtrlStartRatio,
                out CtrlEndRatio);
        }

        /// <summary>
        /// Sets control point ratios to evenly distributed values (1/3 and 2/3).
        /// Useful for straightened edges where control points are linear.
        /// </summary>
        public void SetEvenControlPointRatios() {
            CtrlStartRatio = 1f / 3f;
            CtrlEndRatio   = 2f / 3f;
        }
    }
}
