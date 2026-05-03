namespace NetworkTools.Systems.Tools.RoadShape {
    using System.Collections.Generic;
    using Colossal.Json;
    using Colossal.Mathematics;
    using NetworkTools.Components;
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Parameters;
    using NetworkTools.Systems.Tools.Base;
    using Unity.Collections;
    using Unity.Mathematics;

    /// <summary>
    /// Applies an ease-in-out slope - smooth transitions at start and end.
    /// Uses a bezier curve for height interpolation where the parameter t = path ratio.
    /// Control points determine the transition zones:
    /// - EaseInLength: how far along the path the slope starts to increase
    /// - EaseOutLength: how far from the end the slope starts to level off
    /// Implements IHandleableTransformation to provide draggable control point handles.
    /// </summary>
    public struct SlopeEaseInOutTransform : IPathTransformation {
        /// <summary>
        /// Reference bezier curve used to sample heights at path ratios.
        /// Built in PreProcess with control points positioned to create the ease-in-out shape.
        /// </summary>
        public Bezier4x3 ReferenceBezier;

        public void InitializeConfig(in ShapeTransformContext ctx, ref ShapeJobConfig config) {
            // EaseInOut uses simple normalized parameters (0-0.5) stored in config.
            // No additional computed state needed - handles read directly from config.EaseInLength/EaseOutLength
        }

        public void PreProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeJobConfig config) {
            // Build a 2D height curve where x = path ratio (0-1) and y = height.
            // We solve for t where x(t) = pathRatio to get the correct height.
            //
            // Control point placement creates the S-curve:
            // - a: x=0, y=startHeight
            // - b: x=easeInLength, y=startHeight (flat tangent at start)
            // - c: x=(1-easeOutLength), y=endHeight (flat tangent at end)
            // - d: x=1, y=endHeight
            var a = new float3(0f, ctx.StartHeight, 0f);
            var b = new float3(config.EaseInLength, ctx.StartHeight, 0f);
            var c = new float3(1f - config.EaseOutLength, ctx.EndPosition.y, 0f);
            var d = new float3(1f, ctx.EndPosition.y, 0f);

            ReferenceBezier = new Bezier4x3(a, b, c, d);
        }

        public void Process(ref EdgeState edge, int index, in ShapeTransformContext ctx, in ShapeJobConfig config) {
            // Get heights at the actual node positions (start and end of edge in path order)
            var startHeight = SlopeUtils.GetHeightAtPathRatio(ReferenceBezier, edge.StartPointAbsoluteRatio);
            var endHeight = SlopeUtils.GetHeightAtPathRatio(ReferenceBezier, edge.EndPointAbsoluteRatio);

            // Get slopes at node positions - this ensures tangent continuity at shared nodes
            // Slope is in units of "height per path ratio" (not per world distance)
            var startSlope = SlopeUtils.GetSlopeAtPathRatio(ReferenceBezier, edge.StartPointAbsoluteRatio);
            var endSlope = SlopeUtils.GetSlopeAtPathRatio(ReferenceBezier, edge.EndPointAbsoluteRatio);

            // Calculate control point heights using path ratio differences (NOT world XZ distance)
            // The slope is dHeight/dPathRatio, so we multiply by the path ratio difference
            var ctrlStartHeight = startHeight + startSlope * (edge.StartControlPointAbsoluteRatio - edge.StartPointAbsoluteRatio);
            var ctrlEndHeight = endHeight + endSlope * (edge.EndControlPointAbsoluteRatio - edge.EndPointAbsoluteRatio);

            edge.Bezier = SlopeUtils.ApplyHeightsToBezier(
                edge.Bezier,
                startHeight,
                ctrlStartHeight,
                ctrlEndHeight,
                endHeight,
                edge.IsForward);
        }

        public void PostProcess(ref NativeArray<EdgeState> edges, in ShapeTransformContext ctx, in ShapeJobConfig config) {
            // No post-processing needed
        }

        /// <summary>
        /// Returns handle definitions for ease-in and ease-out control points.
        /// Handles are constrained to move along the direction of their respective edge segments.
        /// </summary>
        public static TransformHandleDefinition[] BuildHandleDefinitions(
            in ShapeTransformContext ctx,
            in ShapeJobConfig config,
            float3 pathStartPos,
            float3 pathEndPos,
            in NativeArray<EdgeState> edgeStates,
            IReadOnlyDictionary<string, ParameterBase> parameters) {

            if (edgeStates.Length == 0) {
                return System.Array.Empty<TransformHandleDefinition>();
            }

            // Get the first and last edge for direction calculation
            var firstEdge = edgeStates[0];
            var lastEdge = edgeStates[^1];

            // Calculate ease-in direction from first edge's bezier (flattened to XZ plane)
            // For forward edges: direction is a->b, for reversed: direction is d->c
            float3 easeInDirection;
            if (firstEdge.IsForward) {
                easeInDirection = firstEdge.Bezier.b - firstEdge.Bezier.a;
            } else {
                easeInDirection = firstEdge.Bezier.c - firstEdge.Bezier.d;
            }
            easeInDirection.y = 0f;
            easeInDirection = math.normalizesafe(easeInDirection);

            // Calculate ease-out direction from last edge's bezier (flattened to XZ plane, pointing backwards from end)
            // For forward edges: direction is d->c, for reversed: direction is a->b
            float3 easeOutDirection;
            if (lastEdge.IsForward) {
                easeOutDirection = lastEdge.Bezier.c - lastEdge.Bezier.d;
            } else {
                easeOutDirection = lastEdge.Bezier.b - lastEdge.Bezier.a;
            }
            easeOutDirection.y = 0f;
            easeOutDirection = math.normalizesafe(easeOutDirection);

            // Calculate path length in XZ for distance calculations
            var pathLengthXZ = math.distance(pathStartPos.xz, pathEndPos.xz);
            var halfPathLength = ctx.TotalLength * 0.5f;

            // Elevate handles above the path for visibility
            var elevation = new float3(0, 1f, 0);

            // Position handles along their respective edge directions (not the straight start-to-end line)
            // The constraint axis uses the edge direction, so the position must match
            var easeInDistance = config.EaseInLength * pathLengthXZ;
            var easeInPos = pathStartPos + easeInDirection * easeInDistance;
            easeInPos.y = pathStartPos.y + elevation.y;

            var easeOutDistance = config.EaseOutLength * pathLengthXZ;
            var easeOutPos = pathEndPos + easeOutDirection * easeOutDistance;
            easeOutPos.y = pathEndPos.y + elevation.y;

            return new[] {
                new TransformHandleDefinition {
                    Key = 1,
                    Position = easeInPos,
                    TypeFlags = HandleTypeFlags.SlopeControl | HandleTypeFlags.Parameter | HandleTypeFlags.ParameterRange,
                    Value = config.EaseInLength,
                    MinValue = 0f,
                    MaxValue = config.EaseInMax,
                    // Constrain to first edge direction with distance clamping (0 to halfPathLength)
                    Constraints = NT_HandleConstraints.AxisWithBounds(easeInDirection, pathStartPos + elevation, 0f, halfPathLength),
                    Parameter = parameters["roadShape.easeInLength"]
                },
                new TransformHandleDefinition {
                    Key = 2,
                    Position = easeOutPos,
                    TypeFlags = HandleTypeFlags.SlopeControl | HandleTypeFlags.Parameter | HandleTypeFlags.ParameterRange,
                    Value = config.EaseOutLength,
                    MinValue = 0f,
                    MaxValue = config.EaseOutMax,
                    // Constrain to last edge direction (reversed) with distance clamping (0 to halfPathLength)
                    Constraints = NT_HandleConstraints.AxisWithBounds(easeOutDirection, pathEndPos + elevation, 0f, halfPathLength),
                    Parameter = parameters["roadShape.easeOutLength"]
                },
            };
        }
    }
}
