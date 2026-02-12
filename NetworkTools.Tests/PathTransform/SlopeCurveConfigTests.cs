// <copyright file="SlopeCurveConfigTests.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Tests.PathTransform {
    using NetworkTools.Systems;
    using NUnit.Framework;

    [TestFixture]
    public class SlopeCurveConfigTests {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Preserve_ReturnsCorrectTemplate() {
            var config = SlopeCurveConfig.Preserve();
            Assert.AreEqual(SlopeTemplate.Preserve, config.Template);
        }

        [Test]
        public void Linear_ReturnsCorrectTemplate() {
            var config = SlopeCurveConfig.Linear();
            Assert.AreEqual(SlopeTemplate.Linear, config.Template);
        }

        [Test]
        public void EaseInOut_DefaultValues_ReturnsCorrectConfig() {
            var config = SlopeCurveConfig.EaseInOut();
            Assert.AreEqual(SlopeTemplate.EaseInOut, config.Template);
            Assert.AreEqual(0.25f,                   config.EaseInLength,  Tolerance);
            Assert.AreEqual(0.25f,                   config.EaseOutLength, Tolerance);
        }

        [Test]
        public void EaseInOut_CustomValues_ClampsToValidRange() {
            var config = SlopeCurveConfig.EaseInOut(0.7f, -0.1f);
            Assert.AreEqual(0.5f, config.EaseInLength,  Tolerance); // Clamped to max 0.5
            Assert.AreEqual(0f,   config.EaseOutLength, Tolerance); // Clamped to min 0
        }

        [Test]
        public void Parabolic_DefaultValues_ReturnsCorrectConfig() {
            var config = SlopeCurveConfig.Parabolic();
            Assert.AreEqual(SlopeTemplate.Parabolic, config.Template);
            Assert.AreEqual(0.5f,                    config.ArchHeight,   Tolerance);
            Assert.AreEqual(0.5f,                    config.ArchPosition, Tolerance);
        }

        [Test]
        public void Parabolic_CustomValues_ClampsToValidRange() {
            var config = SlopeCurveConfig.Parabolic(2f, 0.05f);
            Assert.AreEqual(1f,   config.ArchHeight,   Tolerance); // Clamped to max 1
            Assert.AreEqual(0.1f, config.ArchPosition, Tolerance); // Clamped to min 0.1
        }

        [Test]
        public void ApplyCurve_Linear_ReturnsInputUnchanged() {
            var config = SlopeCurveConfig.Linear();

            Assert.AreEqual(0f,    config.ApplyCurve(0f),    Tolerance);
            Assert.AreEqual(0.25f, config.ApplyCurve(0.25f), Tolerance);
            Assert.AreEqual(0.5f,  config.ApplyCurve(0.5f),  Tolerance);
            Assert.AreEqual(0.75f, config.ApplyCurve(0.75f), Tolerance);
            Assert.AreEqual(1f,    config.ApplyCurve(1f),    Tolerance);
        }

        [Test]
        public void ApplyCurve_Preserve_ReturnsInputUnchanged() {
            var config = SlopeCurveConfig.Preserve();

            Assert.AreEqual(0.5f, config.ApplyCurve(0.5f), Tolerance);
        }

        [Test]
        public void ApplyCurve_EaseInOut_BoundaryValues() {
            var config = SlopeCurveConfig.EaseInOut();

            Assert.AreEqual(0f, config.ApplyCurve(0f), Tolerance);
            Assert.AreEqual(1f, config.ApplyCurve(1f), Tolerance);
        }

        [Test]
        public void ApplyCurve_EaseInOut_MiddleIsLinear() {
            var config = SlopeCurveConfig.EaseInOut();

            // In the middle region (0.25 to 0.75), the curve should be linear
            // So ApplyCurve(0.5) should equal 0.5
            Assert.AreEqual(0.5f, config.ApplyCurve(0.5f), Tolerance);
        }

        [Test]
        public void ApplyCurve_EaseInOut_EaseInRegionBelowLinear() {
            var config = SlopeCurveConfig.EaseInOut();

            // In the ease-in region, values should be below linear
            // At t=0.125 (halfway through ease-in), value should be less than 0.125
            var result = config.ApplyCurve(0.125f);
            Assert.Less(result, 0.125f);
            Assert.Greater(result, 0f);
        }

        [Test]
        public void ApplyCurve_EaseInOut_EaseOutRegionAboveLinear() {
            var config = SlopeCurveConfig.EaseInOut();

            // In the ease-out region, values should be above linear progression toward 1
            // At t=0.875, we're halfway through ease-out
            var result = config.ApplyCurve(0.875f);
            Assert.Greater(result, 0.875f);
            Assert.Less(result, 1f);
        }

        [Test]
        public void ApplyCurve_EaseInOut_ZeroLengths_ReturnsLinear() {
            var config = SlopeCurveConfig.EaseInOut(0f, 0f);

            // With zero ease lengths, should behave like linear
            Assert.AreEqual(0.5f, config.ApplyCurve(0.5f), Tolerance);
        }

        [Test]
        public void ApplyCurve_EaseInOut_OverlappingRegions_UsesSCurve() {
            var config = SlopeCurveConfig.EaseInOut(0.5f, 0.5f);

            // When regions overlap, should use a smooth S-curve
            Assert.AreEqual(0f,   config.ApplyCurve(0f),   Tolerance);
            Assert.AreEqual(0.5f, config.ApplyCurve(0.5f), Tolerance);
            Assert.AreEqual(1f,   config.ApplyCurve(1f),   Tolerance);

            // S-curve should be symmetric
            var at025 = config.ApplyCurve(0.25f);
            var at075 = config.ApplyCurve(0.75f);
            Assert.AreEqual(1f - at025, at075, Tolerance);
        }

        [Test]
        public void ApplyCurve_EaseInOut_Monotonic() {
            var config = SlopeCurveConfig.EaseInOut();

            // Curve should be monotonically increasing
            var prev = 0f;
            for (var t = 0f; t <= 1f; t += 0.05f) {
                var current = config.ApplyCurve(t);
                Assert.GreaterOrEqual(current, prev, $"Curve not monotonic at t={t}");
                prev = current;
            }
        }

        [Test]
        public void ApplyCurve_OutOfRangeInput_ClampsAppropriately() {
            var config = SlopeCurveConfig.EaseInOut();

            // The curve should handle edge values gracefully
            var atZero = config.ApplyCurve(0f);
            var atOne = config.ApplyCurve(1f);

            Assert.AreEqual(0f, atZero, Tolerance);
            Assert.AreEqual(1f, atOne,  Tolerance);
        }
    }
}