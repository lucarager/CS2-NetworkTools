// <copyright file="ShapeCalculatorTests.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Tests.PathTransform {
    using Colossal.Mathematics;
    using NetworkTools.Systems;
    using NUnit.Framework;
    using Unity.Mathematics;

    [TestFixture]
    public class ShapeCalculatorTests {
        private const float Tolerance = 0.0001f;

        #region CalculatePositionLinear

        [Test]
        public void CalculatePositionLinear_AtStart_ReturnsStartXZ() {
            float2 result = ShapeCalculator.CalculatePositionLinear(
                distance: 0f,
                totalLength: 100f,
                startXZ: new float2(0f, 0f),
                endXZ: new float2(100f, 100f));

            Assert.AreEqual(0f, result.x, Tolerance);
            Assert.AreEqual(0f, result.y, Tolerance);
        }

        [Test]
        public void CalculatePositionLinear_AtEnd_ReturnsEndXZ() {
            float2 result = ShapeCalculator.CalculatePositionLinear(
                distance: 100f,
                totalLength: 100f,
                startXZ: new float2(0f, 0f),
                endXZ: new float2(100f, 100f));

            Assert.AreEqual(100f, result.x, Tolerance);
            Assert.AreEqual(100f, result.y, Tolerance);
        }

        [Test]
        public void CalculatePositionLinear_AtMidpoint_ReturnsInterpolated() {
            float2 result = ShapeCalculator.CalculatePositionLinear(
                distance: 50f,
                totalLength: 100f,
                startXZ: new float2(0f, 0f),
                endXZ: new float2(100f, 100f));

            Assert.AreEqual(50f, result.x, Tolerance);
            Assert.AreEqual(50f, result.y, Tolerance);
        }

        [Test]
        public void CalculatePositionLinear_NonOriginStart_CorrectInterpolation() {
            float2 result = ShapeCalculator.CalculatePositionLinear(
                distance: 50f,
                totalLength: 100f,
                startXZ: new float2(100f, 200f),
                endXZ: new float2(200f, 400f));

            Assert.AreEqual(150f, result.x, Tolerance);
            Assert.AreEqual(300f, result.y, Tolerance);
        }

        [Test]
        public void CalculatePositionLinear_ClampsBeyondEnd() {
            float2 result = ShapeCalculator.CalculatePositionLinear(
                distance: 150f, // Beyond total length
                totalLength: 100f,
                startXZ: new float2(0f, 0f),
                endXZ: new float2(100f, 100f));

            // Should clamp to end position
            Assert.AreEqual(100f, result.x, Tolerance);
            Assert.AreEqual(100f, result.y, Tolerance);
        }

        #endregion

        #region CalculateControlPointRatios

        [Test]
        public void CalculateControlPointRatios_StraightLine_Forward() {
            // Straight horizontal bezier
            var bezier = new Bezier4x3 {
                a = new float3(0f, 0f, 0f),
                b = new float3(33.33f, 0f, 0f),
                c = new float3(66.67f, 0f, 0f),
                d = new float3(100f, 0f, 0f),
            };

            ShapeCalculator.CalculateControlPointRatios(
                bezier, 100f, isForward: true,
                out float ctrlStartRatio, out float ctrlEndRatio);

            Assert.AreEqual(1f / 3f, ctrlStartRatio, 0.01f);
            Assert.AreEqual(2f / 3f, ctrlEndRatio, 0.01f);
        }

        [Test]
        public void CalculateControlPointRatios_Reversed_SwapsRatios() {
            // Straight horizontal bezier
            var bezier = new Bezier4x3 {
                a = new float3(0f, 0f, 0f),
                b = new float3(33.33f, 0f, 0f),
                c = new float3(66.67f, 0f, 0f),
                d = new float3(100f, 0f, 0f),
            };

            ShapeCalculator.CalculateControlPointRatios(
                bezier, 100f, isForward: false,
                out float ctrlStartRatio, out float ctrlEndRatio);

            // Reversed: C is closer to path-start, B is closer to path-end
            // ctrlStartRatio = 1 - cRatio = 1 - 0.667 = 0.333
            // ctrlEndRatio = 1 - bRatio = 1 - 0.333 = 0.667
            Assert.AreEqual(1f / 3f, ctrlStartRatio, 0.01f);
            Assert.AreEqual(2f / 3f, ctrlEndRatio, 0.01f);
        }

        [Test]
        public void CalculateControlPointRatios_ZeroLength_ReturnsDefaults() {
            var bezier = new Bezier4x3 {
                a = new float3(0f, 0f, 0f),
                b = new float3(0f, 0f, 0f),
                c = new float3(0f, 0f, 0f),
                d = new float3(0f, 0f, 0f),
            };

            ShapeCalculator.CalculateControlPointRatios(
                bezier, 0f, isForward: true,
                out float ctrlStartRatio, out float ctrlEndRatio);

            Assert.AreEqual(1f / 3f, ctrlStartRatio, Tolerance);
            Assert.AreEqual(2f / 3f, ctrlEndRatio, Tolerance);
        }

        #endregion

        #region CalculateStraightenedPositions

        [Test]
        public void CalculateStraightenedPositions_AllPointsOnLine() {
            var positions = ShapeCalculator.CalculateStraightenedPositions(
                cumulativeDistance: 0f,
                edgeLength: 100f,
                ctrlStartRatio: 1f / 3f,
                ctrlEndRatio: 2f / 3f,
                totalLength: 100f,
                pathStartXZ: new float2(0f, 0f),
                pathEndXZ: new float2(100f, 100f));

            // All points should lie on the line y = x
            Assert.AreEqual(positions.Start.x, positions.Start.y, Tolerance);
            Assert.AreEqual(positions.CtrlStart.x, positions.CtrlStart.y, Tolerance);
            Assert.AreEqual(positions.CtrlEnd.x, positions.CtrlEnd.y, Tolerance);
            Assert.AreEqual(positions.End.x, positions.End.y, Tolerance);
        }

        [Test]
        public void CalculateStraightenedPositions_CorrectDistances() {
            var positions = ShapeCalculator.CalculateStraightenedPositions(
                cumulativeDistance: 0f,
                edgeLength: 100f,
                ctrlStartRatio: 1f / 3f,
                ctrlEndRatio: 2f / 3f,
                totalLength: 100f,
                pathStartXZ: new float2(0f, 0f),
                pathEndXZ: new float2(100f, 0f));

            Assert.AreEqual(0f, positions.Start.x, Tolerance);
            Assert.AreEqual(100f / 3f, positions.CtrlStart.x, Tolerance);
            Assert.AreEqual(200f / 3f, positions.CtrlEnd.x, Tolerance);
            Assert.AreEqual(100f, positions.End.x, Tolerance);
        }

        [Test]
        public void CalculateStraightenedPositions_MidPath_CorrectOffset() {
            // Second edge in a 2-edge path
            var positions = ShapeCalculator.CalculateStraightenedPositions(
                cumulativeDistance: 50f,
                edgeLength: 50f,
                ctrlStartRatio: 1f / 3f,
                ctrlEndRatio: 2f / 3f,
                totalLength: 100f,
                pathStartXZ: new float2(0f, 0f),
                pathEndXZ: new float2(100f, 0f));

            Assert.AreEqual(50f, positions.Start.x, Tolerance);
            Assert.AreEqual(50f + 50f / 3f, positions.CtrlStart.x, Tolerance);
            Assert.AreEqual(50f + 100f / 3f, positions.CtrlEnd.x, Tolerance);
            Assert.AreEqual(100f, positions.End.x, Tolerance);
        }

        #endregion

        #region ApplyPositionsToBezier

        [Test]
        public void ApplyPositionsToBezier_Forward_CorrectAssignment() {
            var bezier = CreateTestBezier();
            var positions = new EdgePositions {
                Start = new float2(10f, 20f),
                CtrlStart = new float2(30f, 40f),
                CtrlEnd = new float2(50f, 60f),
                End = new float2(70f, 80f),
            };

            var result = ShapeCalculator.ApplyPositionsToBezier(bezier, positions, isForward: true);

            Assert.AreEqual(10f, result.a.x, Tolerance);
            Assert.AreEqual(20f, result.a.z, Tolerance);
            Assert.AreEqual(30f, result.b.x, Tolerance);
            Assert.AreEqual(40f, result.b.z, Tolerance);
            Assert.AreEqual(50f, result.c.x, Tolerance);
            Assert.AreEqual(60f, result.c.z, Tolerance);
            Assert.AreEqual(70f, result.d.x, Tolerance);
            Assert.AreEqual(80f, result.d.z, Tolerance);
        }

        [Test]
        public void ApplyPositionsToBezier_Reversed_SwapsAssignment() {
            var bezier = CreateTestBezier();
            var positions = new EdgePositions {
                Start = new float2(10f, 20f),
                CtrlStart = new float2(30f, 40f),
                CtrlEnd = new float2(50f, 60f),
                End = new float2(70f, 80f),
            };

            var result = ShapeCalculator.ApplyPositionsToBezier(bezier, positions, isForward: false);

            // Reversed: a=End, b=CtrlEnd, c=CtrlStart, d=Start
            Assert.AreEqual(70f, result.a.x, Tolerance);
            Assert.AreEqual(80f, result.a.z, Tolerance);
            Assert.AreEqual(50f, result.b.x, Tolerance);
            Assert.AreEqual(60f, result.b.z, Tolerance);
            Assert.AreEqual(30f, result.c.x, Tolerance);
            Assert.AreEqual(40f, result.c.z, Tolerance);
            Assert.AreEqual(10f, result.d.x, Tolerance);
            Assert.AreEqual(20f, result.d.z, Tolerance);
        }

        [Test]
        public void ApplyPositionsToBezier_PreservesY() {
            var bezier = CreateTestBezier();
            var positions = new EdgePositions {
                Start = new float2(999f, 999f),
                CtrlStart = new float2(999f, 999f),
                CtrlEnd = new float2(999f, 999f),
                End = new float2(999f, 999f),
            };

            var result = ShapeCalculator.ApplyPositionsToBezier(bezier, positions, isForward: true);

            // Y values should remain unchanged
            Assert.AreEqual(bezier.a.y, result.a.y, Tolerance);
            Assert.AreEqual(bezier.b.y, result.b.y, Tolerance);
            Assert.AreEqual(bezier.c.y, result.c.y, Tolerance);
            Assert.AreEqual(bezier.d.y, result.d.y, Tolerance);
        }

        #endregion

        #region EvaluateBezier

        [Test]
        public void EvaluateBezier_AtT0_ReturnsP0() {
            float2 p0 = new float2(0f, 0f);
            float2 p1 = new float2(10f, 20f);
            float2 p2 = new float2(30f, 40f);
            float2 p3 = new float2(50f, 50f);

            float2 result = ShapeCalculator.EvaluateBezier(p0, p1, p2, p3, 0f);

            Assert.AreEqual(p0.x, result.x, Tolerance);
            Assert.AreEqual(p0.y, result.y, Tolerance);
        }

        [Test]
        public void EvaluateBezier_AtT1_ReturnsP3() {
            float2 p0 = new float2(0f, 0f);
            float2 p1 = new float2(10f, 20f);
            float2 p2 = new float2(30f, 40f);
            float2 p3 = new float2(50f, 50f);

            float2 result = ShapeCalculator.EvaluateBezier(p0, p1, p2, p3, 1f);

            Assert.AreEqual(p3.x, result.x, Tolerance);
            Assert.AreEqual(p3.y, result.y, Tolerance);
        }

        [Test]
        public void EvaluateBezier_StraightLine_ReturnsLinearInterpolation() {
            // Straight line bezier (control points on line)
            float2 p0 = new float2(0f, 0f);
            float2 p1 = new float2(25f, 25f);
            float2 p2 = new float2(75f, 75f);
            float2 p3 = new float2(100f, 100f);

            float2 result = ShapeCalculator.EvaluateBezier(p0, p1, p2, p3, 0.5f);

            // For a straight line, midpoint should be at (50, 50)
            Assert.AreEqual(50f, result.x, 1f); // Allow some tolerance for curve calculation
            Assert.AreEqual(50f, result.y, 1f);
        }

        #endregion

        #region EvaluateBezierTangent

        [Test]
        public void EvaluateBezierTangent_AtT0_PointsToP1() {
            float2 p0 = new float2(0f, 0f);
            float2 p1 = new float2(10f, 0f);
            float2 p2 = new float2(20f, 0f);
            float2 p3 = new float2(30f, 0f);

            float2 tangent = ShapeCalculator.EvaluateBezierTangent(p0, p1, p2, p3, 0f);

            // At t=0, tangent = 3*(p1-p0) = 3*(10,0) = (30,0)
            Assert.AreEqual(30f, tangent.x, Tolerance);
            Assert.AreEqual(0f, tangent.y, Tolerance);
        }

        [Test]
        public void EvaluateBezierTangent_AtT1_PointsFromP2() {
            float2 p0 = new float2(0f, 0f);
            float2 p1 = new float2(10f, 0f);
            float2 p2 = new float2(20f, 0f);
            float2 p3 = new float2(30f, 0f);

            float2 tangent = ShapeCalculator.EvaluateBezierTangent(p0, p1, p2, p3, 1f);

            // At t=1, tangent = 3*(p3-p2) = 3*(10,0) = (30,0)
            Assert.AreEqual(30f, tangent.x, Tolerance);
            Assert.AreEqual(0f, tangent.y, Tolerance);
        }

        #endregion

        #region GetBezierTangentXZ

        [Test]
        public void GetBezierTangentXZ_Forward_Start_ReturnsAtoB() {
            var bezier = new Bezier4x3 {
                a = new float3(0f, 0f, 0f),
                b = new float3(10f, 5f, 20f),
                c = new float3(30f, 10f, 40f),
                d = new float3(50f, 15f, 60f),
            };

            float2 tangent = ShapeCalculator.GetBezierTangentXZ(bezier, atStart: true, isForward: true);

            // Should be (b.x - a.x, b.z - a.z) = (10, 20)
            Assert.AreEqual(10f, tangent.x, Tolerance);
            Assert.AreEqual(20f, tangent.y, Tolerance);
        }

        [Test]
        public void GetBezierTangentXZ_Forward_End_ReturnsCtoD() {
            var bezier = new Bezier4x3 {
                a = new float3(0f, 0f, 0f),
                b = new float3(10f, 5f, 20f),
                c = new float3(30f, 10f, 40f),
                d = new float3(50f, 15f, 60f),
            };

            float2 tangent = ShapeCalculator.GetBezierTangentXZ(bezier, atStart: false, isForward: true);

            // Should be (d.x - c.x, d.z - c.z) = (20, 20)
            Assert.AreEqual(20f, tangent.x, Tolerance);
            Assert.AreEqual(20f, tangent.y, Tolerance);
        }

        [Test]
        public void GetBezierTangentXZ_Reversed_Start_ReturnsDtoC() {
            var bezier = new Bezier4x3 {
                a = new float3(0f, 0f, 0f),
                b = new float3(10f, 5f, 20f),
                c = new float3(30f, 10f, 40f),
                d = new float3(50f, 15f, 60f),
            };

            float2 tangent = ShapeCalculator.GetBezierTangentXZ(bezier, atStart: true, isForward: false);

            // Should be (c.x - d.x, c.z - d.z) = (-20, -20)
            Assert.AreEqual(-20f, tangent.x, Tolerance);
            Assert.AreEqual(-20f, tangent.y, Tolerance);
        }

        #endregion

        #region CalculateMasterBezierControls

        [Test]
        public void CalculateMasterBezierControls_SymmetricPath_SymmetricControls() {
            float2 startXZ = new float2(0f, 0f);
            float2 endXZ = new float2(100f, 0f);
            float2 startTangent = new float2(1f, 0f); // Pointing right
            float2 endTangent = new float2(1f, 0f);   // Pointing right

            ShapeCalculator.CalculateMasterBezierControls(
                startXZ, endXZ, startTangent, endTangent, 100f,
                out float2 ctrl1, out float2 ctrl2);

            // Control distance should be 1/3 of total length
            float expectedDistance = 100f / 3f;

            // ctrl1 should be start + normalized(startTangent) * distance
            Assert.AreEqual(expectedDistance, ctrl1.x, Tolerance);
            Assert.AreEqual(0f, ctrl1.y, Tolerance);

            // ctrl2 should be end - normalized(endTangent) * distance
            Assert.AreEqual(100f - expectedDistance, ctrl2.x, Tolerance);
            Assert.AreEqual(0f, ctrl2.y, Tolerance);
        }

        [Test]
        public void CalculateMasterBezierControls_AngledPath_CorrectControls() {
            float2 startXZ = new float2(0f, 0f);
            float2 endXZ = new float2(100f, 100f);
            float2 startTangent = new float2(1f, 0f); // Pointing right
            float2 endTangent = new float2(0f, 1f);   // Pointing up

            ShapeCalculator.CalculateMasterBezierControls(
                startXZ, endXZ, startTangent, endTangent, 100f,
                out float2 ctrl1, out float2 ctrl2);

            // ctrl1 should extend right from start
            Assert.Greater(ctrl1.x, 0f);
            Assert.AreEqual(0f, ctrl1.y, Tolerance);

            // ctrl2 should extend down from end (opposite of up tangent)
            Assert.AreEqual(100f, ctrl2.x, Tolerance);
            Assert.Less(ctrl2.y, 100f);
        }

        #endregion

        #region Helper Methods

        private static Bezier4x3 CreateTestBezier() {
            return new Bezier4x3 {
                a = new float3(0f, 0f, 0f),
                b = new float3(33f, 5f, 10f),
                c = new float3(66f, 10f, 20f),
                d = new float3(100f, 15f, 30f),
            };
        }

        #endregion
    }
}
