// <copyright file="TransformContextTests.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Tests.PathTransform {
    using NetworkTools.Systems.Tools.PathTransform;
    using NUnit.Framework;
    using Unity.Mathematics;

    [TestFixture]
    public class TransformContextTests {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Create_SetsStartPositionCorrectly() {
            var startPos = new float3(10f, 20f,  30f);
            var endPos = new float3(100f,  200f, 300f);
            var config = TransformConfig.Preserve();

            var ctx = TransformContext.Create(startPos, endPos, config);

            Assert.AreEqual(10f, ctx.StartPosition.x, Tolerance);
            Assert.AreEqual(20f, ctx.StartPosition.y, Tolerance);
            Assert.AreEqual(30f, ctx.StartPosition.z, Tolerance);
        }

        [Test]
        public void Create_SetsEndPositionCorrectly() {
            var startPos = new float3(10f, 20f,  30f);
            var endPos = new float3(100f,  200f, 300f);
            var config = TransformConfig.Preserve();

            var ctx = TransformContext.Create(startPos, endPos, config);

            Assert.AreEqual(100f, ctx.EndPosition.x, Tolerance);
            Assert.AreEqual(200f, ctx.EndPosition.y, Tolerance);
            Assert.AreEqual(300f, ctx.EndPosition.z, Tolerance);
        }

        [Test]
        public void Create_InitializesTotalLengthToZero() {
            var ctx = TransformContext.Create(float3.zero, float3.zero, TransformConfig.Preserve());

            Assert.AreEqual(0f, ctx.TotalLength, Tolerance);
        }

        [Test]
        public void Create_StoresConfig() {
            var config = TransformConfig.SlopeOnly(SlopeCurveConfig.Linear());

            var ctx = TransformContext.Create(float3.zero, float3.zero, config);

            Assert.AreEqual(SlopeTemplate.Linear, ctx.Config.Slope.Template);
        }

        [Test]
        public void StartHeight_ReturnsYComponent() {
            var ctx = TransformContext.Create(new float3(0f, 42f, 0f),
                new float3(0f,                               0f,  0f),
                TransformConfig.Preserve());

            Assert.AreEqual(42f, ctx.StartHeight, Tolerance);
        }

        [Test]
        public void DeltaHeight_ReturnsCorrectDifference() {
            var ctx = TransformContext.Create(new float3(0f, 10f, 0f),
                new float3(0f,                               50f, 0f),
                TransformConfig.Preserve());

            Assert.AreEqual(40f, ctx.DeltaHeight, Tolerance);
        }

        [Test]
        public void DeltaHeight_NegativeSlope_ReturnsNegative() {
            var ctx = TransformContext.Create(new float3(0f, 100f, 0f),
                new float3(0f,                               20f,  0f),
                TransformConfig.Preserve());

            Assert.AreEqual(-80f, ctx.DeltaHeight, Tolerance);
        }

        [Test]
        public void StartXZ_ReturnsXZComponents() {
            var ctx = TransformContext.Create(new float3(10f, 999f, 30f),
                float3.zero,
                TransformConfig.Preserve());

            Assert.AreEqual(10f, ctx.StartXZ.x, Tolerance);
            Assert.AreEqual(30f, ctx.StartXZ.y, Tolerance);
        }

        [Test]
        public void EndXZ_ReturnsXZComponents() {
            var ctx = TransformContext.Create(float3.zero,
                new float3(100f, 999f, 300f),
                TransformConfig.Preserve());

            Assert.AreEqual(100f, ctx.EndXZ.x, Tolerance);
            Assert.AreEqual(300f, ctx.EndXZ.y, Tolerance);
        }

        [Test]
        public void IsValid_ZeroLength_ReturnsFalse() {
            var ctx = TransformContext.Create(float3.zero, float3.zero, TransformConfig.Preserve());
            ctx.TotalLength = 0f;

            Assert.IsFalse(ctx.IsValid);
        }

        [Test]
        public void IsValid_PositiveLength_ReturnsTrue() {
            var ctx = TransformContext.Create(float3.zero, float3.zero, TransformConfig.Preserve());
            ctx.TotalLength = 100f;

            Assert.IsTrue(ctx.IsValid);
        }

        [Test]
        public void IsValid_NegativeLength_ReturnsFalse() {
            var ctx = TransformContext.Create(float3.zero, float3.zero, TransformConfig.Preserve());
            ctx.TotalLength = -1f;

            Assert.IsFalse(ctx.IsValid);
        }
    }
}