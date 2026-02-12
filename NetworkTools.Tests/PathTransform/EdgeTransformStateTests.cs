// <copyright file="EdgeTransformStateTests.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Tests.PathTransform {
    using Colossal.Mathematics;
    using NetworkTools.Systems;
    using NUnit.Framework;
    using Unity.Mathematics;

    [TestFixture]
    public class EdgeTransformStateTests {
        private const float Tolerance = 0.0001f;

        [Test]
        public void SetEvenControlPointRatios_SetsOneThird() {
            var state = new EdgeTransformState {
                CtrlStartRatio = 0f,
                CtrlEndRatio   = 0f
            };

            state.SetEvenControlPointRatios();

            Assert.AreEqual(1f / 3f, state.CtrlStartRatio, Tolerance);
        }

        [Test]
        public void SetEvenControlPointRatios_SetsTwoThirds() {
            var state = new EdgeTransformState {
                CtrlStartRatio = 0f,
                CtrlEndRatio   = 0f
            };

            state.SetEvenControlPointRatios();

            Assert.AreEqual(2f / 3f, state.CtrlEndRatio, Tolerance);
        }

        [Test]
        public void SetEvenControlPointRatios_OverwritesExistingValues() {
            var state = new EdgeTransformState {
                CtrlStartRatio = 0.1f,
                CtrlEndRatio   = 0.9f
            };

            state.SetEvenControlPointRatios();

            Assert.AreEqual(1f / 3f, state.CtrlStartRatio, Tolerance);
            Assert.AreEqual(2f / 3f, state.CtrlEndRatio,   Tolerance);
        }

        [Test]
        public void CalculateLength_StraightHorizontalLine_ReturnsCorrectLength() {
            var state = new EdgeTransformState {
                Bezier = new Bezier4x3 {
                    a = new float3(0f,     0f, 0f),
                    b = new float3(33.33f, 0f, 0f),
                    c = new float3(66.67f, 0f, 0f),
                    d = new float3(100f,   0f, 0f)
                }
            };

            state.CalculateLength();

            // For a straight line bezier, length should be approximately the distance
            Assert.AreEqual(100f, state.Length, 1f);
        }

        [Test]
        public void CalculateLength_DiagonalLine_ReturnsCorrectLength() {
            var state = new EdgeTransformState {
                Bezier = new Bezier4x3 {
                    a = new float3(0f,     0f,     0f),
                    b = new float3(33.33f, 33.33f, 0f),
                    c = new float3(66.67f, 66.67f, 0f),
                    d = new float3(100f,   100f,   0f)
                }
            };

            state.CalculateLength();

            // Diagonal of 100x100 = sqrt(2) * 100 ≈ 141.42
            Assert.AreEqual(141.42f, state.Length, 2f);
        }

        [Test]
        public void CalculateLength_CurvedBezier_LongerThanStraightLine() {
            var straightState = new EdgeTransformState {
                Bezier = new Bezier4x3 {
                    a = new float3(0f,   0f, 0f),
                    b = new float3(33f,  0f, 0f),
                    c = new float3(66f,  0f, 0f),
                    d = new float3(100f, 0f, 0f)
                }
            };

            var curvedState = new EdgeTransformState {
                Bezier = new Bezier4x3 {
                    a = new float3(0f,   0f,  0f),
                    b = new float3(0f,   50f, 0f), // Control points curve upward
                    c = new float3(100f, 50f, 0f),
                    d = new float3(100f, 0f,  0f)
                }
            };

            straightState.CalculateLength();
            curvedState.CalculateLength();

            Assert.Greater(curvedState.Length, straightState.Length);
        }

        [Test]
        public void RecalculateControlPointRatios_Forward_CorrectRatios() {
            var state = new EdgeTransformState {
                Bezier = new Bezier4x3 {
                    a = new float3(0f,   0f, 0f),
                    b = new float3(25f,  0f, 0f),
                    c = new float3(75f,  0f, 0f),
                    d = new float3(100f, 0f, 0f)
                },
                Length    = 100f,
                IsForward = true
            };

            state.RecalculateControlPointRatios();

            Assert.AreEqual(0.25f, state.CtrlStartRatio, 0.01f);
            Assert.AreEqual(0.75f, state.CtrlEndRatio,   0.01f);
        }

        [Test]
        public void RecalculateControlPointRatios_Reversed_CorrectRatios() {
            var state = new EdgeTransformState {
                Bezier = new Bezier4x3 {
                    a = new float3(0f,   0f, 0f),
                    b = new float3(25f,  0f, 0f),
                    c = new float3(75f,  0f, 0f),
                    d = new float3(100f, 0f, 0f)
                },
                Length    = 100f,
                IsForward = false
            };

            state.RecalculateControlPointRatios();

            // Reversed: C is at 0.75 from start, B is at 0.25
            // ctrlStartRatio = 1 - 0.75 = 0.25
            // ctrlEndRatio = 1 - 0.25 = 0.75
            Assert.AreEqual(0.25f, state.CtrlStartRatio, 0.01f);
            Assert.AreEqual(0.75f, state.CtrlEndRatio,   0.01f);
        }

        [Test]
        public void Integration_SlopeCalculator_UseStateOverload() {
            var state = new EdgeTransformState {
                Bezier             = CreateTestBezier(),
                Length             = 100f,
                CumulativeDistance = 0f,
                IsForward          = true
            };
            state.SetEvenControlPointRatios();

            var ctx = TransformContext.Create(new float3(0f, 0f,   0f),
                new float3(100f,                             100f, 0f),
                TransformConfig.SlopeOnly(SlopeCurveConfig.Linear()));
            ctx.TotalLength = 100f;

            var heights = SlopeCalculator.CalculateEdgeHeights(in state, in ctx);

            Assert.AreEqual(0f,   heights.Start, Tolerance);
            Assert.AreEqual(100f, heights.End,   Tolerance);
        }

        [Test]
        public void Integration_ShapeCalculator_UseStateOverload() {
            var state = new EdgeTransformState {
                Bezier             = CreateTestBezier(),
                Length             = 100f,
                CumulativeDistance = 0f,
                IsForward          = true
            };
            state.SetEvenControlPointRatios();

            var ctx = TransformContext.Create(new float3(0f, 0f, 0f),
                new float3(100f,                             0f, 100f),
                TransformConfig.ShapeOnly(ShapeCurveConfig.Straighten()));
            ctx.TotalLength = 100f;

            var positions = ShapeCalculator.CalculateStraightenedPositions(in state, in ctx);

            Assert.AreEqual(0f,   positions.Start.x, Tolerance);
            Assert.AreEqual(0f,   positions.Start.y, Tolerance);
            Assert.AreEqual(100f, positions.End.x,   Tolerance);
            Assert.AreEqual(100f, positions.End.y,   Tolerance);
        }

        private static Bezier4x3 CreateTestBezier() {
            return new Bezier4x3 {
                a = new float3(0f,   0f,  0f),
                b = new float3(33f,  5f,  10f),
                c = new float3(66f,  10f, 20f),
                d = new float3(100f, 15f, 30f)
            };
        }
    }
}