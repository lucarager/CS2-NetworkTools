// <copyright file="TransformConfigTests.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Tests.PathTransform {
    using NetworkTools.Systems;
    using NUnit.Framework;

    [TestFixture]
    public class TransformConfigTests {
        #region Factory Methods

        [Test]
        public void Preserve_BothTemplatesArePreserve() {
            var config = TransformConfig.Preserve();

            Assert.AreEqual(ShapeTemplate.Preserve, config.Shape.Template);
            Assert.AreEqual(SlopeTemplate.Preserve, config.Slope.Template);
            Assert.AreEqual(TransformFlags.None, config.Flags);
        }

        [Test]
        public void SlopeOnly_ShapeIsPreserve() {
            var config = TransformConfig.SlopeOnly(SlopeCurveConfig.Linear());

            Assert.AreEqual(ShapeTemplate.Preserve, config.Shape.Template);
            Assert.AreEqual(SlopeTemplate.Linear, config.Slope.Template);
        }

        [Test]
        public void ShapeOnly_SlopeIsPreserve() {
            var config = TransformConfig.ShapeOnly(ShapeCurveConfig.Straighten());

            Assert.AreEqual(ShapeTemplate.Straighten, config.Shape.Template);
            Assert.AreEqual(SlopeTemplate.Preserve, config.Slope.Template);
        }

        [Test]
        public void Combined_BothTemplatesSet() {
            var config = TransformConfig.Combined(
                ShapeCurveConfig.Smooth(0.5f),
                SlopeCurveConfig.EaseInOut(0.2f, 0.3f));

            Assert.AreEqual(ShapeTemplate.Smooth, config.Shape.Template);
            Assert.AreEqual(SlopeTemplate.EaseInOut, config.Slope.Template);
        }

        #endregion

        #region HasTransform

        [Test]
        public void HasTransform_BothPreserve_ReturnsFalse() {
            var config = TransformConfig.Preserve();

            Assert.IsFalse(config.HasTransform);
        }

        [Test]
        public void HasTransform_ShapeActive_ReturnsTrue() {
            var config = TransformConfig.ShapeOnly(ShapeCurveConfig.Straighten());

            Assert.IsTrue(config.HasTransform);
        }

        [Test]
        public void HasTransform_SlopeActive_ReturnsTrue() {
            var config = TransformConfig.SlopeOnly(SlopeCurveConfig.Linear());

            Assert.IsTrue(config.HasTransform);
        }

        [Test]
        public void HasTransform_BothActive_ReturnsTrue() {
            var config = TransformConfig.Combined(
                ShapeCurveConfig.Straighten(),
                SlopeCurveConfig.Linear());

            Assert.IsTrue(config.HasTransform);
        }

        #endregion

        #region HasShapeTransform

        [Test]
        public void HasShapeTransform_Preserve_ReturnsFalse() {
            var config = TransformConfig.Preserve();

            Assert.IsFalse(config.HasShapeTransform);
        }

        [Test]
        public void HasShapeTransform_Straighten_ReturnsTrue() {
            var config = TransformConfig.ShapeOnly(ShapeCurveConfig.Straighten());

            Assert.IsTrue(config.HasShapeTransform);
        }

        [Test]
        public void HasShapeTransform_Smooth_ReturnsTrue() {
            var config = TransformConfig.ShapeOnly(ShapeCurveConfig.Smooth());

            Assert.IsTrue(config.HasShapeTransform);
        }

        #endregion

        #region HasSlopeTransform

        [Test]
        public void HasSlopeTransform_Preserve_ReturnsFalse() {
            var config = TransformConfig.Preserve();

            Assert.IsFalse(config.HasSlopeTransform);
        }

        [Test]
        public void HasSlopeTransform_Linear_ReturnsTrue() {
            var config = TransformConfig.SlopeOnly(SlopeCurveConfig.Linear());

            Assert.IsTrue(config.HasSlopeTransform);
        }

        [Test]
        public void HasSlopeTransform_EaseInOut_ReturnsTrue() {
            var config = TransformConfig.SlopeOnly(SlopeCurveConfig.EaseInOut());

            Assert.IsTrue(config.HasSlopeTransform);
        }

        [Test]
        public void HasSlopeTransform_Parabolic_ReturnsTrue() {
            var config = TransformConfig.SlopeOnly(SlopeCurveConfig.Parabolic());

            Assert.IsTrue(config.HasSlopeTransform);
        }

        #endregion
    }
}
