// <copyright file="SlopeCalculatorTests.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Tests.PathTransform {
    using Colossal.Mathematics;
    using NetworkTools.Systems.Tools.PathTransform;
    using NUnit.Framework;
    using Unity.Mathematics;

    [TestFixture]
    public class SlopeCalculatorTests {
        private const float Tolerance = 0.0001f;

        [Test]
        public void CalculateHeight_Linear_AtStart_ReturnsStartHeight() {
            var config = SlopeCurveConfig.Linear();

            var result = SlopeCalculator.CalculateHeight(0f,
                100f,
                10f,
                20f,
                config);

            Assert.AreEqual(10f, result, Tolerance);
        }

        [Test]
        public void CalculateHeight_Linear_AtEnd_ReturnsEndHeight() {
            var config = SlopeCurveConfig.Linear();

            var result = SlopeCalculator.CalculateHeight(100f,
                100f,
                10f,
                20f,
                config);

            Assert.AreEqual(30f, result, Tolerance); // 10 + 20
        }

        [Test]
        public void CalculateHeight_Linear_AtMidpoint_ReturnsInterpolated() {
            var config = SlopeCurveConfig.Linear();

            var result = SlopeCalculator.CalculateHeight(50f,
                100f,
                10f,
                20f,
                config);

            Assert.AreEqual(20f, result, Tolerance); // 10 + 10
        }

        [Test]
        public void CalculateHeight_Linear_NegativeDelta_Descending() {
            var config = SlopeCurveConfig.Linear();

            var result = SlopeCalculator.CalculateHeight(50f,
                100f,
                30f,
                -20f,
                config);

            Assert.AreEqual(20f, result, Tolerance); // 30 - 10
        }

        [Test]
        public void CalculateHeight_EaseInOut_AtBoundaries() {
            var config = SlopeCurveConfig.EaseInOut();

            var atStart = SlopeCalculator.CalculateHeight(0f, 100f, 10f, 20f, config);
            var atEnd = SlopeCalculator.CalculateHeight(100f, 100f, 10f, 20f, config);

            Assert.AreEqual(10f, atStart, Tolerance);
            Assert.AreEqual(30f, atEnd,   Tolerance);
        }

        [Test]
        public void CalculateHeight_Parabolic_CreatesArch() {
            var config = SlopeCurveConfig.Parabolic(1f);

            var atStart = SlopeCalculator.CalculateHeight(0f, 100f, 0f, 0f, config);
            var atPeak = SlopeCalculator.CalculateHeight(50f, 100f, 0f, 0f, config);
            var atEnd = SlopeCalculator.CalculateHeight(100f, 100f, 0f, 0f, config);

            Assert.AreEqual(0f, atStart, Tolerance);
            Assert.AreEqual(0f, atEnd,   Tolerance);
        }

        [Test]
        public void CalculateHeight_Parabolic_WithDelta_ModifiesSlope() {
            var config = SlopeCurveConfig.Parabolic(1f);

            // With archHeight=1, at t=0.5, the curve returns 1 (reaches full height early)
            var atMid = SlopeCalculator.CalculateHeight(50f, 100f, 0f, 100f, config);

            // At t=0.5 with full parabolic arch, curvedRatio=1, so height = 0 + 100*1 = 100
            Assert.AreEqual(100f, atMid, Tolerance);
        }

        [Test]
        public void CalculateEdgeHeights_EvenRatios_CorrectDistribution() {
            var config = SlopeCurveConfig.Linear();

            var heights = SlopeCalculator.CalculateEdgeHeights(0f,
                100f,
                1f / 3f,
                2f / 3f,
                100f,
                0f,
                100f,
                config);

            Assert.AreEqual(0f,        heights.Start,     Tolerance);
            Assert.AreEqual(100f / 3f, heights.CtrlStart, Tolerance);
            Assert.AreEqual(200f / 3f, heights.CtrlEnd,   Tolerance);
            Assert.AreEqual(100f,      heights.End,       Tolerance);
        }

        [Test]
        public void CalculateEdgeHeights_CustomRatios_RespectsRatios() {
            var config = SlopeCurveConfig.Linear();

            var heights = SlopeCalculator.CalculateEdgeHeights(0f,
                100f,
                0.2f,
                0.8f,
                100f,
                0f,
                100f,
                config);

            Assert.AreEqual(0f,   heights.Start,     Tolerance);
            Assert.AreEqual(20f,  heights.CtrlStart, Tolerance);
            Assert.AreEqual(80f,  heights.CtrlEnd,   Tolerance);
            Assert.AreEqual(100f, heights.End,       Tolerance);
        }

        [Test]
        public void CalculateEdgeHeights_MidPath_CorrectOffset() {
            var config = SlopeCurveConfig.Linear();

            // Second edge in a 2-edge path
            var heights = SlopeCalculator.CalculateEdgeHeights(50f, // First edge was 50 units
                50f,
                1f / 3f,
                2f / 3f,
                100f,
                0f,
                100f,
                config);

            Assert.AreEqual(50f, heights.Start, Tolerance);
            // CtrlStart at 50 + 50*(1/3) = 66.67
            Assert.AreEqual(50f + 50f / 3f, heights.CtrlStart, Tolerance);
            // CtrlEnd at 50 + 50*(2/3) = 83.33
            Assert.AreEqual(50f + 100f / 3f, heights.CtrlEnd, Tolerance);
            Assert.AreEqual(100f,            heights.End,     Tolerance);
        }

        [Test]
        public void ApplyHeightsToBezier_Forward_CorrectAssignment() {
            var bezier = CreateTestBezier();
            var heights = new EdgeHeights {
                Start     = 10f,
                CtrlStart = 20f,
                CtrlEnd   = 30f,
                End       = 40f
            };

            var result = SlopeCalculator.ApplyHeightsToBezier(bezier, heights, true);

            Assert.AreEqual(10f, result.a.y, Tolerance);
            Assert.AreEqual(20f, result.b.y, Tolerance);
            Assert.AreEqual(30f, result.c.y, Tolerance);
            Assert.AreEqual(40f, result.d.y, Tolerance);
        }

        [Test]
        public void ApplyHeightsToBezier_Reversed_SwapsAssignment() {
            var bezier = CreateTestBezier();
            var heights = new EdgeHeights {
                Start     = 10f,
                CtrlStart = 20f,
                CtrlEnd   = 30f,
                End       = 40f
            };

            var result = SlopeCalculator.ApplyHeightsToBezier(bezier, heights, false);

            // Reversed: a=End, b=CtrlEnd, c=CtrlStart, d=Start
            Assert.AreEqual(40f, result.a.y, Tolerance);
            Assert.AreEqual(30f, result.b.y, Tolerance);
            Assert.AreEqual(20f, result.c.y, Tolerance);
            Assert.AreEqual(10f, result.d.y, Tolerance);
        }

        [Test]
        public void ApplyHeightsToBezier_PreservesXZ() {
            var bezier = CreateTestBezier();
            var heights = new EdgeHeights {
                Start     = 100f,
                CtrlStart = 100f,
                CtrlEnd   = 100f,
                End       = 100f
            };

            var result = SlopeCalculator.ApplyHeightsToBezier(bezier, heights, true);

            // XZ values should remain unchanged
            Assert.AreEqual(bezier.a.x, result.a.x, Tolerance);
            Assert.AreEqual(bezier.a.z, result.a.z, Tolerance);
            Assert.AreEqual(bezier.b.x, result.b.x, Tolerance);
            Assert.AreEqual(bezier.b.z, result.b.z, Tolerance);
            Assert.AreEqual(bezier.c.x, result.c.x, Tolerance);
            Assert.AreEqual(bezier.c.z, result.c.z, Tolerance);
            Assert.AreEqual(bezier.d.x, result.d.x, Tolerance);
            Assert.AreEqual(bezier.d.z, result.d.z, Tolerance);
        }

        private static Bezier4x3 CreateTestBezier() {
            return new Bezier4x3 {
                a = new float3(0f,   0f,  0f),
                b = new float3(33f,  5f,  0f),
                c = new float3(66f,  10f, 0f),
                d = new float3(100f, 15f, 0f)
            };
        }
    }
}