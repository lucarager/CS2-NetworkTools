namespace NetworkTools.Tests.Tunnel {
    using System.Collections.Generic;

    using Colossal.Mathematics;

    using Game.Net;
    using Game.Simulation;

    using NetworkTools.Systems.Tools.Utils;

    using NUnit.Framework;

    using Unity.Collections;
    using Unity.Mathematics;

    using Lane = NetworkTools.Tests.ProvingGround.Lane;

    /// <summary>
    ///     Tests of <see cref="TunnelRuns" /> on the <see cref="ProvingGround" />.
    ///     The terrain is the one the game holds once it has loaded the ground.
    ///     A level road runs along the middle of a lane, at the height of the flat ground.
    ///     The mouths expected here are the ones a test in the running game expects too.
    /// </summary>
    [TestFixture]
    public class ProvingGroundTests {
        /// <summary>
        ///     Half the width of a small road.
        /// </summary>
        private const float HalfWidth = 8f;

        /// <summary>
        ///     Elevation limit of a road.
        /// </summary>
        private const float Limit = 4f;

        /// <summary>
        ///     Distance a mouth may miss its expected point by.
        /// </summary>
        private const float MouthTolerance = 0.15f;

        /// <summary>
        ///     The proving ground, built once for all the tests.
        /// </summary>
        private TestTerrain m_Terrain;

        /// <summary>
        ///     Builds the proving ground before the first test.
        /// </summary>
        [OneTimeSetUp]
        public void BuildTerrain() {
            m_Terrain = TestTerrain.OfProvingGround();
        }

        /// <summary>
        ///     Releases the proving ground after the last test.
        /// </summary>
        [OneTimeTearDown]
        public void ReleaseTerrain() {
            m_Terrain.Dispose();
        }

        [Test]
        public void Terrain_AtAPost_IsTheFormula() {
            var lanes = new[] { Lane.Ridge, Lane.Staircase, Lane.Ravines, Lane.Pit, Lane.Sawtooth };

            foreach (var lane in lanes) {
                for (var u = 0f; u <= ProvingGround.LaneLength; u += 24.5f) {
                    var point  = ProvingGround.Point(lane, u, 7f);
                    var height = TerrainUtils.SampleHeight(ref m_Terrain.Data, point);
                    var relief = ProvingGround.Relief(lane, u, 7f);

                    Assert.AreEqual(ProvingGround.Base + relief, height, 0.005f, $"{lane} at {u}");
                }
            }
        }

        [Test]
        public void Terrain_Anywhere_IsTheHeightFormula() {
            var centre = ProvingGround.DomeCentre().xz;
            var points = new[] {
                centre + new float2(200f, 0f),
                centre + new float2(-150f, 120f),
                centre + new float2(30f, -270f),
                centre + new float2(-300f, 0f),
                ProvingGround.Point(Lane.Hipped, 490f, 46f).xz,
                ProvingGround.Point(Lane.Gentle, 300f, -80f).xz,
                ProvingGround.Point(Lane.Sawtooth, 1040f, 0f).xz,
            };

            foreach (var point in points) {
                var position = new float3(point.x, 0f, point.y);
                var height   = TerrainUtils.SampleHeight(ref m_Terrain.Data, position);

                Assert.AreEqual(ProvingGround.Height(point), height, 0.02f, $"{point}");
            }
        }

        [Test]
        public void Terrain_BetweenLanes_IsFlat() {
            var point = ProvingGround.Point(Lane.Ridge, 400f, ProvingGround.LaneHalfWidth + 35f);

            Assert.AreEqual(
                ProvingGround.Base,
                TerrainUtils.SampleHeight(ref m_Terrain.Data, point),
                0.005f);
        }

        [Test]
        public void Ridge_MouthsOutWhereTheFlanksHaveTwoLimitsAndAHalf() {
            AssertTunnels(Lane.Ridge);
            AssertTunnels(Lane.Hipped);
            AssertTunnels(Lane.Deep);
        }

        [Test]
        public void Ridge_RoadHigherOrLower_MouthsFollowTheFlanks() {
            NativeAllocations.Require();

            foreach (var lift in new[] { -4f, 6f }) {
                var rise    = new float3(0f, lift, 0f);
                var start   = ProvingGround.Point(Lane.Ridge, 41f, 0f) + rise;
                var end     = ProvingGround.Point(Lane.Ridge, 1011f, 0f) + rise;
                var mouths  = ProvingGround.RidgeMouths(lift);
                var tunnels = Tunnels(NetUtils.StraightCurve(start, end));

                Assert.AreEqual(1, tunnels.Count, Describe(tunnels));
                Assert.AreEqual(mouths[0], tunnels[0].min, MouthTolerance, $"{lift}: in");
                Assert.AreEqual(mouths[1], tunnels[0].max, MouthTolerance, $"{lift}: out");
            }
        }

        [Test]
        public void RidgeOnTiltedGround_MouthsStayWhereTheLowSideHasThreeLimits() {
            AssertTunnels(Lane.RidgeTiltLeft);
            AssertTunnels(Lane.RidgeTiltRight);
        }

        [Test]
        public void Staircase_MouthWithinReachOfThreeLimits() {
            NativeAllocations.Require();

            var tunnels = Tunnels(Road(Lane.Staircase));
            var mouthIn = ProvingGround.Mouths(Lane.Staircase, HalfWidth)[0];

            // The cover is 12 m from the post at 700 m, and falls from 13 m to none before 840 m.
            // The mouth in goes out on the 11 m plateau as far as a road reaches.
            // The mouth out is no bisection: it keeps a portal edge from the end of the stretch.
            Assert.AreEqual(1, tunnels.Count, Describe(tunnels));
            Assert.AreEqual(mouthIn, tunnels[0].min, MouthTolerance);
            Assert.That(tunnels[0].max, Is.InRange(826f, 836.5f + 3.5f / 13f));
        }

        [Test]
        public void Ravines_UnderOneLimitOrLongUnderThreeBreakTheTunnel() {
            AssertTunnels(Lane.Ravines);
        }

        [Test]
        public void Gentle_MouthsFarFromTheFoot() {
            AssertTunnels(Lane.Gentle);
        }

        [Test]
        public void Hillside_LowSideNeverCovered_NoTunnel() {
            AssertTunnels(Lane.Hillside);
        }

        [Test]
        public void Pit_RoadOverTheGround_IsNoRunUnderIt() {
            AssertTunnels(Lane.Pit);
        }

        [Test]
        public void Twins_EachRidgeItsTunnel() {
            AssertTunnels(Lane.Twins);
        }

        [Test]
        public void Sawtooth_EightTunnels() {
            AssertTunnels(Lane.Sawtooth);
        }

        [Test]
        public void Humps_ATunnelNeedsThreeLimitsAndTwoSamplesOnceMovedOut() {
            NativeAllocations.Require();

            var tunnels = Tunnels(Road(Lane.Humps));
            var last    = tunnels.Count - 1;

            // The hump with 12 m at its top alone may go either way, as a sample falls on it.
            // The others have 12 m over 4 m or more, and flanks rising one in two.
            // Each mouth goes out 7 m from 12 m of cover.
            Assert.That(tunnels.Count, Is.InRange(5, 6), Describe(tunnels));
            Assert.Greater(tunnels[0].min, 300f);
            Assert.AreEqual(425f, tunnels[last - 4].min, MouthTolerance);
            Assert.AreEqual(443f, tunnels[last - 4].max, MouthTolerance);
            Assert.AreEqual(535f, tunnels[last - 3].min, MouthTolerance);
            Assert.AreEqual(557f, tunnels[last - 3].max, MouthTolerance);
            Assert.AreEqual(643f, tunnels[last - 2].min, MouthTolerance);
            Assert.AreEqual(673f, tunnels[last - 2].max, MouthTolerance);
            Assert.AreEqual(751f, tunnels[last - 1].min, MouthTolerance);
            Assert.AreEqual(789f, tunnels[last - 1].max, MouthTolerance);
            Assert.AreEqual(874.5f, tunnels[last].min, MouthTolerance);
            Assert.AreEqual(945.5f, tunnels[last].max, MouthTolerance);
        }

        [Test]
        public void Cliff_MouthInKeepsAPortalEdgeFromTheFoot() {
            NativeAllocations.Require();

            foreach (var lane in new[] { Lane.Cliff, Lane.CliffSkew }) {
                var tunnels = Tunnels(Road(lane));

                // A mouth keeps a portal edge from the foot: on a cliff it has more than it needs.
                Assert.AreEqual(1, tunnels.Count, $"{lane}: {Describe(tunnels)}");
                Assert.That(tunnels[0].min, Is.InRange(208f, 240f), $"{lane}");
                Assert.AreEqual(823f, tunnels[0].max, MouthTolerance, $"{lane}");
            }
        }

        [Test]
        public void Dome_RoadThroughTheMiddle_MouthsOnTheFlank() {
            NativeAllocations.Require();

            var centre  = ProvingGround.DomeCentre();
            var reach   = new float3(ProvingGround.DomeRadius + 61f, 0f, 0f);
            var road    = NetUtils.StraightCurve(centre - reach, centre + reach);
            var tunnels = Tunnels(road, centre.x - reach.x);

            // The flank has 8.5 m of cover 17 m from the foot, a little less under the edges.
            Assert.AreEqual(1, tunnels.Count);
            Assert.AreEqual(61f + 17f, tunnels[0].min, 0.5f);
            Assert.AreEqual(61f + 2f * ProvingGround.DomeRadius - 17f, tunnels[0].max, 0.5f);
        }

        [Test]
        public void Dome_Bend_MouthsStayWhereTheWholeWidthHasThreeLimitsOnTheCurve() {
            NativeAllocations.Require();

            var centre = ProvingGround.DomeCentre();
            var bend   = new Bezier4x3(
                centre + new float3(0f, 0f, -400f),
                centre + new float3(0f, 0f, -100f),
                centre + new float3(100f, 0f, 0f),
                centre + new float3(400f, 0f, 0f));
            var mouths = new List<float>();

            foreach (var run in Split(bend)) {
                if (IsTunnel(bend, run)) {
                    mouths.Add(run.min);
                    mouths.Add(run.max);
                }
            }

            // One tunnel, in on one leg and out on the other, at the cover the formulas give.
            // The legs cross the flank 7 degrees from square.
            // Under 12 m of cover, a head wall there would show a step of 1.5 m.
            Assert.AreEqual(2, mouths.Count);
            Assert.Less(MathUtils.Position(bend, mouths[0]).z, centre.z - 200f);
            Assert.Greater(MathUtils.Position(bend, mouths[1]).x, centre.x + 200f);

            foreach (var t in mouths) {
                var position = MathUtils.Position(bend, t);
                var tangent  = MathUtils.Tangent(bend, t);
                var cover    = ProvingGround.Cover(position, tangent, HalfWidth);

                Assert.AreEqual(12f, cover, 0.1f, $"{t}");
                Assert.IsTrue(
                    TunnelRuns.IsMouth(ref m_Terrain.Data, bend, t, HalfWidth, Limit, 0f),
                    $"{t}");
            }
        }

        /// <summary>
        ///     Checks the tunnels of a road along a lane against the mouths the ground expects.
        ///     A test in the running game checks against the same.
        /// </summary>
        /// <param name="lane">The lane.</param>
        private void AssertTunnels(Lane lane) {
            NativeAllocations.Require();

            var mouths  = ProvingGround.Mouths(lane, HalfWidth);
            var tunnels = Tunnels(Road(lane));

            Assert.AreEqual(mouths.Length / 2, tunnels.Count, $"{lane}: tunnels");

            for (var i = 0; i < tunnels.Count; i++) {
                var found = tunnels[i];

                Assert.AreEqual(mouths[i * 2], found.min, MouthTolerance, $"{lane}: in {i}");
                Assert.AreEqual(mouths[i * 2 + 1], found.max, MouthTolerance, $"{lane}: out {i}");
            }
        }

        /// <summary>
        ///     Makes the road of a lane, from 41 m to 1011 m along its middle.
        ///     No sample of the road falls on a post that way.
        /// </summary>
        /// <param name="lane">The lane.</param>
        /// <returns>The curve of the road.</returns>
        private static Bezier4x3 Road(Lane lane) {
            var start = ProvingGround.Point(lane, 41f, 0f);
            var end   = ProvingGround.Point(lane, 1011f, 0f);

            return NetUtils.StraightCurve(start, end);
        }

        /// <summary>
        ///     Checks whether a run is a tunnel, by the rules a tunnel follows.
        ///     Its ends and its middle have two limits of cover, one of them three limits.
        ///     It never has less than one.
        ///     An open cut from the surface fails the first, one between two tunnels the others.
        /// </summary>
        /// <param name="road">The curve.</param>
        /// <param name="run">The run of the curve.</param>
        /// <returns>True if the run is a tunnel.</returns>
        private bool IsTunnel(Bezier4x3 road, Bounds1 run) {
            var deep   = Limit * 2f;
            var middle = MathUtils.Center(run);
            var steps  = (int)math.ceil(MathUtils.Size(run) * MathUtils.Length(road));

            var covers = new float3(
                TunnelRuns.Cover(ref m_Terrain.Data, road, run.min, HalfWidth),
                TunnelRuns.Cover(ref m_Terrain.Data, road, middle, HalfWidth),
                TunnelRuns.Cover(ref m_Terrain.Data, road, run.max, HalfWidth));

            if (math.cmin(covers) < deep || math.cmax(covers) < Limit * 3f - TunnelRuns.Tolerance) {
                return false;
            }

            for (var i = 1; i < steps; i++) {
                var t = math.lerp(run.min, run.max, i / (float)steps);

                if (TunnelRuns.Cover(ref m_Terrain.Data, road, t, HalfWidth) < Limit - 0.01f) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        ///     Writes tunnels out for a failure message.
        /// </summary>
        /// <param name="tunnels">The tunnels.</param>
        /// <returns>Each tunnel's start and end, in order.</returns>
        private static string Describe(List<Bounds1> tunnels) {
            var text = string.Empty;

            foreach (var tunnel in tunnels) {
                text += $"[{tunnel.min:F2} {tunnel.max:F2}] ";
            }

            return text;
        }

        /// <summary>
        ///     Splits a road along a lane and gets its tunnels, as distances along the lane.
        /// </summary>
        /// <param name="road">The curve to split.</param>
        /// <returns>The tunnels, in order.</returns>
        private List<Bounds1> Tunnels(Bezier4x3 road) {
            return Tunnels(road, ProvingGround.Point(Lane.Ridge, 0f, 0f).x);
        }

        /// <summary>
        ///     Splits a road and gets its tunnels, as distances along x from an origin.
        ///     Tunnel runs that follow one another count as one tunnel, as the game's edges do.
        /// </summary>
        /// <param name="road">The curve to split.</param>
        /// <param name="origin">The x the distances start from.</param>
        /// <returns>The tunnels, in order.</returns>
        private List<Bounds1> Tunnels(Bezier4x3 road, float origin) {
            var runs    = Split(road);
            var tunnels = new List<Bounds1>();

            for (var i = 0; i < runs.Length; i++) {
                if (!IsTunnel(road, runs[i])) {
                    continue;
                }

                var tunnel = new Bounds1(
                    MathUtils.Position(road, runs[i].min).x - origin,
                    MathUtils.Position(road, runs[i].max).x - origin);

                if (i > 0 && tunnels.Count > 0 && IsTunnel(road, runs[i - 1])) {
                    tunnel.min = tunnels[tunnels.Count - 1].min;
                    tunnels.RemoveAt(tunnels.Count - 1);
                }

                tunnels.Add(tunnel);
            }

            return tunnels;
        }

        /// <summary>
        ///     Splits a road and copies the runs out of their native list.
        /// </summary>
        /// <param name="road">The curve to split.</param>
        /// <returns>The runs, in order.</returns>
        private Bounds1[] Split(Bezier4x3 road) {
            var runs = new NativeList<Bounds1>(8, Allocator.Temp);

            TunnelRuns.Split(
                ref m_Terrain.Data,
                road,
                MathUtils.Length(road),
                HalfWidth,
                Limit,
                ref runs);

            var result = new Bounds1[runs.Length];

            for (var i = 0; i < result.Length; i++) {
                result[i] = runs[i];
            }

            runs.Dispose();

            return result;
        }
    }
}
