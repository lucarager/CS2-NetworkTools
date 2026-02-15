// <copyright file="PathTransformUtility.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools.PathTransform {
    #region Using Statements

    using Unity.Collections;

    #endregion

    /// <summary>
    /// Pure utility class for path transformation pipeline operations.
    /// Contains all transformation logic without Unity ECS dependencies for easier testing.
    /// </summary>
    public static class PathTransformUtility {
        // ========================================
        // Pipeline Stage 3: Shape Transforms (XZ)
        // ========================================

        /// <summary>
        /// Applies shape transformations to all edges based on the configured template.
        /// </summary>
        public static void ApplyShapeTransforms(NativeArray<EdgeTransformState> edges, in TransformContext ctx) {
            if (!ctx.Config.HasShapeTransform) return;

            switch (ctx.Config.Shape.Template) {
                case ShapeTemplate.Straighten:
                    ApplyStraightenTransform(edges, in ctx);
                    break;
                case ShapeTemplate.Smooth:
                    ApplySmoothTransform(edges, in ctx);
                    break;
            }
        }

        /// <summary>
        /// Array overload for unit testing outside Unity.
        /// </summary>
        public static void ApplyShapeTransforms(EdgeTransformState[] edges, in TransformContext ctx) {
            if (!ctx.Config.HasShapeTransform) return;

            switch (ctx.Config.Shape.Template) {
                case ShapeTemplate.Straighten:
                    ApplyStraightenTransform(edges, in ctx);
                    break;
                case ShapeTemplate.Smooth:
                    ApplySmoothTransform(edges, in ctx);
                    break;
            }
        }

        /// <summary>
        /// Straightens all edges to lie on a direct line from path start to path end.
        /// </summary>
        public static void ApplyStraightenTransform(NativeArray<EdgeTransformState> edges, in TransformContext ctx) {
            for (var i = 0; i < edges.Length; i++) {
                var state     = edges[i];
                var positions = ShapeCalculator.CalculateStraightenedPositions(in state, in ctx);

                state.Bezier = ShapeCalculator.ApplyPositionsToBezier(state.Bezier, positions, state.IsForward);
                state.CalculateLength();
                state.SetEvenControlPointRatios();

                edges[i] = state;
            }
        }

        /// <summary>
        /// Array overload for unit testing outside Unity.
        /// </summary>
        public static void ApplyStraightenTransform(EdgeTransformState[] edges, in TransformContext ctx) {
            for (var i = 0; i < edges.Length; i++) {
                var positions = ShapeCalculator.CalculateStraightenedPositions(in edges[i], in ctx);

                edges[i].Bezier = ShapeCalculator.ApplyPositionsToBezier(edges[i].Bezier, positions, edges[i].IsForward);
                edges[i].CalculateLength();
                edges[i].SetEvenControlPointRatios();
            }
        }

        /// <summary>
        /// Smooths all edges to follow a master bezier curve from path start to path end.
        /// </summary>
        public static void ApplySmoothTransform(NativeArray<EdgeTransformState> edges, in TransformContext ctx) {
            if (edges.Length == 0) return;

            // Calculate master bezier controls from first/last edge tangents
            var firstEdge = edges[0];
            var lastEdge  = edges[^1];

            var startTangent = ShapeCalculator.GetBezierTangentXZ(firstEdge.Bezier, true, firstEdge.IsForward);
            var endTangent   = ShapeCalculator.GetBezierTangentXZ(lastEdge.Bezier, false, lastEdge.IsForward);

            ShapeCalculator.CalculateMasterBezierControls(
                ctx.StartXZ, ctx.EndXZ,
                startTangent, endTangent,
                ctx.TotalLength,
                out var masterCtrl1, out var masterCtrl2);

            // Apply smooth transform to each edge
            for (var i = 0; i < edges.Length; i++) {
                var state     = edges[i];
                var positions = ShapeCalculator.CalculateSmoothedPositions(in state, in ctx, masterCtrl1, masterCtrl2);

                state.Bezier = ShapeCalculator.ApplyPositionsToBezier(state.Bezier, positions, state.IsForward);
                state.CalculateLength();
                state.RecalculateControlPointRatios();

                edges[i] = state;
            }
        }

        /// <summary>
        /// Array overload for unit testing outside Unity.
        /// </summary>
        public static void ApplySmoothTransform(EdgeTransformState[] edges, in TransformContext ctx) {
            if (edges.Length == 0) return;

            var firstEdge = edges[0];
            var lastEdge  = edges[^1];

            var startTangent = ShapeCalculator.GetBezierTangentXZ(firstEdge.Bezier, true, firstEdge.IsForward);
            var endTangent   = ShapeCalculator.GetBezierTangentXZ(lastEdge.Bezier, false, lastEdge.IsForward);

            ShapeCalculator.CalculateMasterBezierControls(
                ctx.StartXZ, ctx.EndXZ,
                startTangent, endTangent,
                ctx.TotalLength,
                out var masterCtrl1, out var masterCtrl2);

            for (var i = 0; i < edges.Length; i++) {
                var positions = ShapeCalculator.CalculateSmoothedPositions(in edges[i], in ctx, masterCtrl1, masterCtrl2);

                edges[i].Bezier = ShapeCalculator.ApplyPositionsToBezier(edges[i].Bezier, positions, edges[i].IsForward);
                edges[i].CalculateLength();
                edges[i].RecalculateControlPointRatios();
            }
        }

        /// <summary>
        /// Recalculates cumulative distances and total path length
        /// after shape transforms have modified the bezier curves.
        /// Assumes each transform has already updated edge lengths.
        /// </summary>
        public static void RecalculateGeometry(NativeArray<EdgeTransformState> edges, ref TransformContext context) {
            var cumulativeDistance = 0f;

            for (var i = 0; i < edges.Length; i++) {
                var state = edges[i];
                state.CumulativeDistance = cumulativeDistance;
                edges[i] = state;
                cumulativeDistance += state.Length;
            }

            context.TotalLength = cumulativeDistance;
        }

        /// <summary>
        /// Array overload for unit testing outside Unity.
        /// </summary>
        public static void RecalculateGeometry(EdgeTransformState[] edges, ref TransformContext context) {
            var cumulativeDistance = 0f;

            for (var i = 0; i < edges.Length; i++) {
                edges[i].CumulativeDistance = cumulativeDistance;
                cumulativeDistance += edges[i].Length;
            }

            context.TotalLength = cumulativeDistance;
        }

        // ========================================
        // Pipeline Stage 4: Slope Transforms (Y)
        // ========================================

        /// <summary>
        /// Applies slope transformations to all edges based on the configured template.
        /// </summary>
        public static void ApplySlopeTransforms(NativeArray<EdgeTransformState> edges, in TransformContext ctx) {
            if (!ctx.Config.HasSlopeTransform) return;

            switch (ctx.Config.Slope.Template) {
                case SlopeTemplate.Linear:
                    ApplyLinearSlopeTransform(edges, in ctx);
                    break;
                case SlopeTemplate.EaseInOut:
                    ApplyEaseInOutSlopeTransform(edges, in ctx);
                    break;
                case SlopeTemplate.Parabolic:
                    ApplyParabolicSlopeTransform(edges, in ctx);
                    break;
            }
        }

        /// <summary>
        /// Array overload for unit testing outside Unity.
        /// </summary>
        public static void ApplySlopeTransforms(EdgeTransformState[] edges, in TransformContext ctx) {
            if (!ctx.Config.HasSlopeTransform) return;

            switch (ctx.Config.Slope.Template) {
                case SlopeTemplate.Linear:
                    ApplyLinearSlopeTransform(edges, in ctx);
                    break;
                case SlopeTemplate.EaseInOut:
                    ApplyEaseInOutSlopeTransform(edges, in ctx);
                    break;
                case SlopeTemplate.Parabolic:
                    ApplyParabolicSlopeTransform(edges, in ctx);
                    break;
            }
        }

        /// <summary>
        /// Applies a linear slope transform - constant slope throughout the path.
        /// </summary>
        public static void ApplyLinearSlopeTransform(NativeArray<EdgeTransformState> edges, in TransformContext ctx) {
            for (var i = 0; i < edges.Length; i++) {
                var state = edges[i];

                // Use even ratios for linear slopes - this produces a constant gradient
                // regardless of control point XZ positions
                state.SetEvenControlPointRatios();

                var heights = SlopeCalculator.CalculateEdgeHeights(in state, in ctx);
                state.Bezier = SlopeCalculator.ApplyHeightsToBezier(state.Bezier, heights, state.IsForward);
                edges[i] = state;
            }
        }

        /// <summary>
        /// Array overload for unit testing outside Unity.
        /// </summary>
        public static void ApplyLinearSlopeTransform(EdgeTransformState[] edges, in TransformContext ctx) {
            for (var i = 0; i < edges.Length; i++) {
                edges[i].SetEvenControlPointRatios();

                var heights = SlopeCalculator.CalculateEdgeHeights(in edges[i], in ctx);
                edges[i].Bezier = SlopeCalculator.ApplyHeightsToBezier(edges[i].Bezier, heights, edges[i].IsForward);
            }
        }

        /// <summary>
        /// Applies an ease-in-out slope transform - smooth transitions at start and end.
        /// </summary>
        public static void ApplyEaseInOutSlopeTransform(NativeArray<EdgeTransformState> edges, in TransformContext ctx) {
            for (var i = 0; i < edges.Length; i++) {
                var state   = edges[i];
                var heights = SlopeCalculator.CalculateEdgeHeights(in state, in ctx);
                state.Bezier = SlopeCalculator.ApplyHeightsToBezier(state.Bezier, heights, state.IsForward);
                edges[i] = state;
            }
        }

        /// <summary>
        /// Array overload for unit testing outside Unity.
        /// </summary>
        public static void ApplyEaseInOutSlopeTransform(EdgeTransformState[] edges, in TransformContext ctx) {
            for (var i = 0; i < edges.Length; i++) {
                var heights = SlopeCalculator.CalculateEdgeHeights(in edges[i], in ctx);
                edges[i].Bezier = SlopeCalculator.ApplyHeightsToBezier(edges[i].Bezier, heights, edges[i].IsForward);
            }
        }

        /// <summary>
        /// Applies a parabolic slope transform - creates an arch (hill) or dip (valley).
        /// </summary>
        public static void ApplyParabolicSlopeTransform(NativeArray<EdgeTransformState> edges, in TransformContext ctx) {
            for (var i = 0; i < edges.Length; i++) {
                var state   = edges[i];
                var heights = SlopeCalculator.CalculateEdgeHeights(in state, in ctx);
                state.Bezier = SlopeCalculator.ApplyHeightsToBezier(state.Bezier, heights, state.IsForward);
                edges[i] = state;
            }
        }

        /// <summary>
        /// Array overload for unit testing outside Unity.
        /// </summary>
        public static void ApplyParabolicSlopeTransform(EdgeTransformState[] edges, in TransformContext ctx) {
            for (var i = 0; i < edges.Length; i++) {
                var heights = SlopeCalculator.CalculateEdgeHeights(in edges[i], in ctx);
                edges[i].Bezier = SlopeCalculator.ApplyHeightsToBezier(edges[i].Bezier, heights, edges[i].IsForward);
            }
        }

        // ========================================
        // Utility Methods
        // ========================================

        /// <summary>
        /// Gets the cumulative distance at a node index (sum of all edge lengths before this node).
        /// </summary>
        public static float GetCumulativeDistanceAtNode(NativeArray<EdgeTransformState> edges, int nodeIndex) {
            if (nodeIndex <= 0) return 0f;
            // The cumulative distance at node i is the cumulative distance of edge i-1 plus its length
            var prevEdge = edges[nodeIndex - 1];
            return prevEdge.CumulativeDistance + prevEdge.Length;
        }

        /// <summary>
        /// Array overload for unit testing outside Unity.
        /// </summary>
        public static float GetCumulativeDistanceAtNode(EdgeTransformState[] edges, int nodeIndex) {
            if (nodeIndex <= 0) return 0f;
            var prevEdge = edges[nodeIndex - 1];
            return prevEdge.CumulativeDistance + prevEdge.Length;
        }
    }
}
