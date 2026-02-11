// <copyright file="PathTransformPipelineTests.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Tests.PathTransform {
    using Colossal.Mathematics;
    using NetworkTools.Systems;
    using NUnit.Framework;
    using Unity.Mathematics;

    /// <summary>
    /// Integration tests that simulate the transformation pipeline flow
    /// without Unity ECS dependencies. Tests the complete data flow from
    /// input beziers through calculators to output beziers.
    /// </summary>
    [TestFixture]
    public class PathTransformPipelineTests {
        private const float Tolerance = 0.01f;

        #region Linear Slope Transform - Single Edge

        [Test]
        public void LinearSlope_SingleEdge_EndpointsMatchPathEndpoints() {
            // Arrange: Create a single edge path
            var startPos = new float3(0f, 10f, 0f);
            var endPos = new float3(100f, 50f, 0f);
            var config = TransformConfig.SlopeOnly(SlopeCurveConfig.Linear());

            var ctx = TransformContext.Create(startPos, endPos, config);
            ctx.TotalLength = 100f;

            var edges = new EdgeTransformState[16];
            var state = CreateEdgeState(CreateHorizontalBezier(0f, 100f), 0f, 100f, true);
            state.SetEvenControlPointRatios();
            edges[0] = state;

            // Act: Apply slope transforms using utility (mimics Execute flow)
            PathTransformUtility.ApplySlopeTransforms(edges, in ctx);

            var resultBezier = edges[0].Bezier;

            // Assert: Endpoints match path start/end heights
            Assert.AreEqual(10f, resultBezier.a.y, Tolerance, "Start height should match path start");
            Assert.AreEqual(50f, resultBezier.d.y, Tolerance, "End height should match path end");
        }

        [Test]
        public void LinearSlope_SingleEdge_ControlPointsInterpolateLinearly() {
            // Arrange
            var ctx = TransformContext.Create(
                new float3(0f, 0f, 0f),
                new float3(100f, 90f, 0f),
                TransformConfig.SlopeOnly(SlopeCurveConfig.Linear()));
            ctx.TotalLength = 100f;

            var edges = new EdgeTransformState[16];
            var state = CreateEdgeState(CreateHorizontalBezier(0f, 100f), 0f, 100f, true);
            state.SetEvenControlPointRatios();
            edges[0] = state;

            // Act: Apply slope transforms using utility (mimics Execute flow)
            PathTransformUtility.ApplySlopeTransforms(edges, in ctx);

            var resultBezier = edges[0].Bezier;

            // Assert: Control points at 1/3 and 2/3 of 90m rise
            Assert.AreEqual(30f, resultBezier.b.y, Tolerance);
            Assert.AreEqual(60f, resultBezier.c.y, Tolerance);
        }

        #endregion

        #region Linear Slope Transform - Multi Edge

        [Test]
        public void LinearSlope_ThreeEdges_ContinuousSlope() {
            // Arrange: Three consecutive edges
            var ctx = TransformContext.Create(
                new float3(0f, 0f, 0f),
                new float3(300f, 90f, 0f),
                TransformConfig.SlopeOnly(SlopeCurveConfig.Linear()));
            ctx.TotalLength = 300f;

            var edges = new EdgeTransformState[16];
            edges[0] = CreateEdgeState(CreateHorizontalBezier(0f, 100f), 0f, 100f, true);
            edges[1] = CreateEdgeState(CreateHorizontalBezier(100f, 200f), 100f, 100f, true);
            edges[2] = CreateEdgeState(CreateHorizontalBezier(200f, 300f), 200f, 100f, true);

            for (int j = 0; j < edges.Length; j++) {
                var state = edges[j];
                state.SetEvenControlPointRatios();
                edges[j] = state;
            }

            // Act: Apply slope transforms using utility (mimics Execute flow)
            PathTransformUtility.ApplySlopeTransforms(edges, in ctx);

            // Assert: Heights are continuous across edges
            Assert.AreEqual(0f, edges[0].Bezier.a.y, Tolerance, "First edge starts at 0");
            Assert.AreEqual(30f, edges[0].Bezier.d.y, Tolerance, "First edge ends at 30");
            Assert.AreEqual(30f, edges[1].Bezier.a.y, Tolerance, "Second edge starts at 30");
            Assert.AreEqual(60f, edges[1].Bezier.d.y, Tolerance, "Second edge ends at 60");
            Assert.AreEqual(60f, edges[2].Bezier.a.y, Tolerance, "Third edge starts at 60");
            Assert.AreEqual(90f, edges[2].Bezier.d.y, Tolerance, "Third edge ends at 90");
        }

        [Test]
        public void LinearSlope_ReversedEdge_CorrectHeightAssignment() {
            // Arrange: Edge with reversed direction
            var ctx = TransformContext.Create(
                new float3(0f, 0f, 0f),
                new float3(100f, 100f, 0f),
                TransformConfig.SlopeOnly(SlopeCurveConfig.Linear()));
            ctx.TotalLength = 100f;

            // Edge is stored with d at path-start, a at path-end (reversed)
            var reversedBezier = new Bezier4x3 {
                a = new float3(100f, 0f, 0f),  // Path end
                b = new float3(66f, 0f, 0f),
                c = new float3(33f, 0f, 0f),
                d = new float3(0f, 0f, 0f),    // Path start
            };

            var edges = new EdgeTransformState[16];
            var state = CreateEdgeState(reversedBezier, 0f, 100f, isForward: false);
            state.SetEvenControlPointRatios();
            edges[0] = state;

            // Act: Apply slope transforms using utility (mimics Execute flow)
            PathTransformUtility.ApplySlopeTransforms(edges, in ctx);

            var resultBezier = edges[0].Bezier;

            // Assert: Heights applied in reversed order
            // a is path-end (height=100), d is path-start (height=0)
            Assert.AreEqual(100f, resultBezier.a.y, Tolerance, "a should have end height");
            Assert.AreEqual(0f, resultBezier.d.y, Tolerance, "d should have start height");
        }

        #endregion

        #region Straighten Transform

        [Test]
        public void Straighten_WavyPath_BecomesLinear() {
            // Arrange: Path that curves in XZ plane
            var ctx = TransformContext.Create(
                new float3(0f, 0f, 0f),
                new float3(100f, 0f, 0f), // Straight line in X
                TransformConfig.ShapeOnly(ShapeCurveConfig.Straighten()));
            ctx.TotalLength = 100f;

            // Edge with a curve in Z direction
            var curvedBezier = new Bezier4x3 {
                a = new float3(0f, 0f, 0f),
                b = new float3(33f, 0f, 30f),   // Curves in Z
                c = new float3(66f, 0f, 30f),
                d = new float3(100f, 0f, 0f),
            };

            var edges = new EdgeTransformState[16];
            var state = CreateEdgeState(curvedBezier, 0f, 100f, true);
            state.SetEvenControlPointRatios();
            edges[0] = state;

            // Act: Apply shape transforms using utility (mimics Execute flow)
            PathTransformUtility.ApplyShapeTransforms(edges, in ctx);

            var resultBezier = edges[0].Bezier;

            // Assert: All points now on the straight line (Z = 0)
            Assert.AreEqual(0f, resultBezier.a.z, Tolerance);
            Assert.AreEqual(0f, resultBezier.b.z, Tolerance);
            Assert.AreEqual(0f, resultBezier.c.z, Tolerance);
            Assert.AreEqual(0f, resultBezier.d.z, Tolerance);
        }

        [Test]
        public void Straighten_PreservesYCoordinates() {
            // Arrange: Path with varying heights
            var ctx = TransformContext.Create(
                new float3(0f, 10f, 0f),
                new float3(100f, 50f, 0f),
                TransformConfig.ShapeOnly(ShapeCurveConfig.Straighten()));
            ctx.TotalLength = 100f;

            var bezier = new Bezier4x3 {
                a = new float3(0f, 10f, 0f),
                b = new float3(33f, 20f, 30f),
                c = new float3(66f, 40f, 30f),
                d = new float3(100f, 50f, 0f),
            };

            var edges = new EdgeTransformState[16];
            var state = CreateEdgeState(bezier, 0f, 100f, true);
            state.SetEvenControlPointRatios();
            edges[0] = state;

            // Act: Apply shape transforms using utility (mimics Execute flow)
            PathTransformUtility.ApplyShapeTransforms(edges, in ctx);

            var resultBezier = edges[0].Bezier;

            // Assert: Y coordinates unchanged
            Assert.AreEqual(10f, resultBezier.a.y, Tolerance);
            Assert.AreEqual(20f, resultBezier.b.y, Tolerance);
            Assert.AreEqual(40f, resultBezier.c.y, Tolerance);
            Assert.AreEqual(50f, resultBezier.d.y, Tolerance);
        }

        #endregion

        #region Combined Transforms

        [Test]
        public void Combined_StraightenAndLinearSlope_BothApplied() {
            // Arrange
            var ctx = TransformContext.Create(
                new float3(0f, 0f, 0f),
                new float3(100f, 100f, 0f),
                TransformConfig.Combined(
                    ShapeCurveConfig.Straighten(),
                    SlopeCurveConfig.Linear()));
            ctx.TotalLength = 100f;

            // Curved bezier with non-linear heights
            var bezier = new Bezier4x3 {
                a = new float3(0f, 5f, 0f),
                b = new float3(33f, 10f, 50f),   // Curves in Z, non-linear Y
                c = new float3(66f, 80f, 50f),
                d = new float3(100f, 90f, 0f),
            };

            var edges = new EdgeTransformState[16];
            var state = CreateEdgeState(bezier, 0f, 100f, true);
            state.SetEvenControlPointRatios();
            edges[0] = state;

            // Act: Follow Execute() pipeline - Shape transforms first
            PathTransformUtility.ApplyShapeTransforms(edges, in ctx);

            // Recalculate geometry after shape transforms (as in Execute flow)
            PathTransformUtility.RecalculateGeometry(edges, ref ctx);

            // Then apply slope transforms
            PathTransformUtility.ApplySlopeTransforms(edges, in ctx);

            var result = edges[0].Bezier;

            // Assert: XZ is straightened
            Assert.AreEqual(0f, result.a.z, Tolerance);
            Assert.AreEqual(0f, result.d.z, Tolerance);

            // Assert: Y is linear from 0 to 100
            Assert.AreEqual(0f, result.a.y, Tolerance);
            Assert.AreEqual(100f, result.d.y, Tolerance);
        }

        #endregion

        #region EaseInOut Slope

        [Test]
        public void EaseInOut_StartSlopeIsGentler() {
            // Arrange
            var config = SlopeCurveConfig.EaseInOut(0.25f, 0.25f);
            var ctx = TransformContext.Create(
                new float3(0f, 0f, 0f),
                new float3(100f, 100f, 0f),
                TransformConfig.SlopeOnly(config));
            ctx.TotalLength = 100f;

            // Create an edge that covers the ease-in region (0-50% of path)
            var edges = new EdgeTransformState[16];
            var state = CreateEdgeState(CreateHorizontalBezier(0f, 50f), 0f, 50f, true);
            state.SetEvenControlPointRatios();
            edges[0] = state;

            // Act: Calculate heights (test the calculator's behavior directly)
            var heights = SlopeCalculator.CalculateEdgeHeights(in state, in ctx);

            // Assert: In ease-in region (at ~16.67% of total path), height gain is less than linear
            // At 16.67% distance (1/3 of this edge), linear would be 16.67, ease-in should be less
            float linearAt1667 = 100f / 6f;
            Assert.Less(heights.CtrlStart, linearAt1667, "Ease-in should produce lower height than linear");
        }

        [Test]
        public void EaseInOut_EndpointsMatch() {
            // Arrange
            var config = SlopeCurveConfig.EaseInOut(0.25f, 0.25f);
            var ctx = TransformContext.Create(
                new float3(0f, 10f, 0f),
                new float3(100f, 90f, 0f),
                TransformConfig.SlopeOnly(config));
            ctx.TotalLength = 100f;

            var edges = new EdgeTransformState[16];
            var state = CreateEdgeState(CreateHorizontalBezier(0f, 100f), 0f, 100f, true);
            state.SetEvenControlPointRatios();
            edges[0] = state;

            // Act: Apply slope transforms using utility (mimics Execute flow)
            PathTransformUtility.ApplySlopeTransforms(edges, in ctx);

            var resultBezier = edges[0].Bezier;

            // Assert: Endpoints still match path start/end
            Assert.AreEqual(10f, resultBezier.a.y, Tolerance);
            Assert.AreEqual(90f, resultBezier.d.y, Tolerance);
        }

        #endregion

        #region Slope Consistency Tests

        [Test]
        public void LinearSlope_FourEdges_ConsistentSlopePercentage() {
            // Arrange: Four edges with varying initial heights (simulating uneven terrain)
            var ctx = TransformContext.Create(
                new float3(0f, 0f, 0f),
                new float3(400f, 80f, 0f),
                TransformConfig.SlopeOnly(SlopeCurveConfig.Linear()));
            ctx.TotalLength = 400f;

            var edges = new EdgeTransformState[16];
            edges[0] = CreateEdgeState(new Bezier4x3 {
                a = new float3(0f, 5f, 0f),
                b = new float3(33f, 8f, 0f),
                c = new float3(66f, 12f, 0f),
                d = new float3(100f, 15f, 0f),
            }, 0f, 100f, true);

            edges[1] = CreateEdgeState(new Bezier4x3 {
                a = new float3(100f, 15f, 0f),
                b = new float3(133f, 10f, 0f),
                c = new float3(166f, 8f, 0f),
                d = new float3(200f, 5f, 0f),
            }, 100f, 100f, true);

            edges[2] = CreateEdgeState(new Bezier4x3 {
                a = new float3(200f, 5f, 0f),
                b = new float3(233f, 20f, 0f),
                c = new float3(266f, 25f, 0f),
                d = new float3(300f, 30f, 0f),
            }, 200f, 100f, true);

            edges[3] = CreateEdgeState(new Bezier4x3 {
                a = new float3(300f, 30f, 0f),
                b = new float3(333f, 35f, 0f),
                c = new float3(366f, 40f, 0f),
                d = new float3(400f, 45f, 0f),
            }, 300f, 100f, true);

            for (int j = 0; j < 4; j++) {
                var state = edges[j];
                state.SetEvenControlPointRatios();
                edges[j] = state;
            }

            // Act: Apply linear slope
            PathTransformUtility.ApplySlopeTransforms(edges, in ctx);

            // Calculate actual slope percentage for each edge (rise/run * 100)
            // Use the actual 3D arc length, not just XZ distance
            var slopes = new float[4];
            for (int i = 0; i < 4; i++) {
                var bezier = edges[i].Bezier;
                float rise = bezier.d.y - bezier.a.y;
                float run = edges[i].Length; 
                slopes[i] = (rise / run) * 100f;
            }

            // Assert: All slopes should be equal (within tolerance)
            float expectedSlope = slopes[0];
            for (int i = 1; i < 4; i++) {
                Assert.AreEqual(expectedSlope, slopes[i], Tolerance, 
                    $"Edge {i} slope should match edge 0 slope");
            }

            // Note: Expected slope percentage depends on total arc length vs height gain
            // With linear slope, all edges should have the same percentage
        }

        [Test]
        public void ShapePlusLinearSlope_FourEdges_ConsistentSlopeAfterStraighten() {
            // Arrange: Four edges with curves in XZ plane (wavy path)
            var ctx = TransformContext.Create(
                new float3(0f, 0f, 0f),
                new float3(400f, 100f, 0f),
                TransformConfig.Combined(
                    ShapeCurveConfig.Straighten(),
                    SlopeCurveConfig.Linear()));
            ctx.TotalLength = 400f;

            var edges = new EdgeTransformState[16];

            // Edge 0: Curves to the right
            edges[0] = CreateEdgeState(new Bezier4x3 {
                a = new float3(0f, 5f, 0f),
                b = new float3(33f, 8f, 20f),
                c = new float3(66f, 12f, 25f),
                d = new float3(100f, 15f, 10f),
            }, 0f, 100f, true);

            // Edge 1: Curves to the left
            edges[1] = CreateEdgeState(new Bezier4x3 {
                a = new float3(100f, 15f, 10f),
                b = new float3(133f, 10f, -15f),
                c = new float3(166f, 8f, -20f),
                d = new float3(200f, 5f, -5f),
            }, 100f, 100f, true);

            // Edge 2: Curves to the right again
            edges[2] = CreateEdgeState(new Bezier4x3 {
                a = new float3(200f, 5f, -5f),
                b = new float3(233f, 20f, 30f),
                c = new float3(266f, 25f, 35f),
                d = new float3(300f, 30f, 15f),
            }, 200f, 100f, true);

            // Edge 3: Straight section
            edges[3] = CreateEdgeState(new Bezier4x3 {
                a = new float3(300f, 30f, 15f),
                b = new float3(333f, 35f, 10f),
                c = new float3(366f, 40f, 5f),
                d = new float3(400f, 45f, 0f),
            }, 300f, 100f, true);

            for (int j = 0; j < 4; j++) {
                var state = edges[j];
                state.SetEvenControlPointRatios();
                edges[j] = state;
            }

            // Act: Apply shape transforms first (straighten)
            PathTransformUtility.ApplyShapeTransforms(edges, in ctx);

            // Recalculate geometry after shape transforms
            PathTransformUtility.RecalculateGeometry(edges, ref ctx);

            // Then apply slope transforms
            PathTransformUtility.ApplySlopeTransforms(edges, in ctx);

            // Calculate actual slope percentage for each edge
            // Use the actual 3D arc length, not just XZ distance
            var slopes = new float[4];
            for (int i = 0; i < 4; i++) {
                var bezier = edges[i].Bezier;
                float rise = bezier.d.y - bezier.a.y;
                float run = edges[i].Length; 
                slopes[i] = (rise / run) * 100f;
            }

            // Assert: All slopes should be equal after straightening and applying linear slope
            float expectedSlope = slopes[0];
            for (int i = 1; i < 4; i++) {
                Assert.AreEqual(expectedSlope, slopes[i], Tolerance, 
                    $"Edge {i} slope ({slopes[i]:F2}%) should match edge 0 slope ({expectedSlope:F2}%) after straighten + linear slope");
            }

            // Assert: All edges should be straight (no Z deviation)
            for (int i = 0; i < 4; i++) {
                var bezier = edges[i].Bezier;
                Assert.AreEqual(0f, bezier.a.z, Tolerance, $"Edge {i} start should be at Z=0");
                Assert.AreEqual(0f, bezier.b.z, Tolerance, $"Edge {i} ctrl1 should be at Z=0");
                Assert.AreEqual(0f, bezier.c.z, Tolerance, $"Edge {i} ctrl2 should be at Z=0");
                Assert.AreEqual(0f, bezier.d.z, Tolerance, $"Edge {i} end should be at Z=0");
            }
        }

        [Test]
        public void ShapePlusLinearSlope_UnevenEdgeLengths_ConsistentSlope() {
            // Arrange: Five edges with different lengths (50, 75, 100, 125, 50)
            var totalLength = 400f;
            var ctx = TransformContext.Create(
                new float3(0f, 0f, 0f),
                new float3(400f, 120f, 0f),
                TransformConfig.Combined(
                    ShapeCurveConfig.Straighten(),
                    SlopeCurveConfig.Linear()));
            ctx.TotalLength = totalLength;

            var edges = new EdgeTransformState[16];

            // Edge 0: 50 units
            edges[0] = CreateEdgeState(new Bezier4x3 {
                a = new float3(0f, 10f, 5f),
                b = new float3(16f, 12f, -8f),
                c = new float3(33f, 15f, -10f),
                d = new float3(50f, 18f, -2f),
            }, 0f, 50f, true);

            // Edge 1: 75 units
            edges[1] = CreateEdgeState(new Bezier4x3 {
                a = new float3(50f, 18f, -2f),
                b = new float3(75f, 25f, 15f),
                c = new float3(100f, 30f, 20f),
                d = new float3(125f, 35f, 8f),
            }, 50f, 75f, true);

            // Edge 2: 100 units
            edges[2] = CreateEdgeState(new Bezier4x3 {
                a = new float3(125f, 35f, 8f),
                b = new float3(158f, 40f, -12f),
                c = new float3(191f, 45f, -15f),
                d = new float3(225f, 50f, -5f),
            }, 125f, 100f, true);

            // Edge 3: 125 units
            edges[3] = CreateEdgeState(new Bezier4x3 {
                a = new float3(225f, 50f, -5f),
                b = new float3(266f, 55f, 18f),
                c = new float3(308f, 60f, 22f),
                d = new float3(350f, 65f, 10f),
            }, 225f, 125f, true);

            // Edge 4: 50 units
            edges[4] = CreateEdgeState(new Bezier4x3 {
                a = new float3(350f, 65f, 10f),
                b = new float3(366f, 70f, 5f),
                c = new float3(383f, 75f, 2f),
                d = new float3(400f, 80f, 0f),
            }, 350f, 50f, true);

            for (int j = 0; j < 5; j++) {
                var state = edges[j];
                state.SetEvenControlPointRatios();
                edges[j] = state;
            }

            // Act: Apply shape then slope transforms
            PathTransformUtility.ApplyShapeTransforms(edges, in ctx);
            PathTransformUtility.RecalculateGeometry(edges, ref ctx);
            PathTransformUtility.ApplySlopeTransforms(edges, in ctx);

            // Calculate actual slope percentage for each edge
            // Use the actual 3D arc length, not just XZ distance
            var slopes = new float[5];
            for (int i = 0; i < 5; i++) {
                var bezier = edges[i].Bezier;
                float rise = bezier.d.y - bezier.a.y;
                float run = edges[i].Length; 
                slopes[i] = (rise / run) * 100f;
            }

            // Assert: All slopes should be equal despite different edge lengths
            float expectedSlope = slopes[0];
            for (int i = 1; i < 5; i++) {
                Assert.AreEqual(expectedSlope, slopes[i], Tolerance, 
                    $"Edge {i} (length={edges[i].Length}) slope ({slopes[i]:F2}%) should match edge 0 slope ({expectedSlope:F2}%)");
            }

            // Note: After straightening, the 3D arc length changes from the original horizontal distance
            // The important thing is that all edges have consistent slope percentage
        }

        #endregion

        #region Helper Methods

        private static EdgeTransformState CreateEdgeState(Bezier4x3 bezier, float cumDist, float length, bool isForward) {
            return new EdgeTransformState {
                Bezier = bezier,
                CumulativeDistance = cumDist,
                Length = length,
                IsForward = isForward,
            };
        }

        private static Bezier4x3 CreateHorizontalBezier(float startX, float endX) {
            float length = endX - startX;
            return new Bezier4x3 {
                a = new float3(startX, 0f, 0f),
                b = new float3(startX + length / 3f, 0f, 0f),
                c = new float3(startX + 2f * length / 3f, 0f, 0f),
                d = new float3(endX, 0f, 0f),
            };
        }

        #endregion
    }
}
