namespace NetworkTools.Tests.Tunnel {
    using Colossal.Mathematics;

    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;

    using NetworkTools.Systems.Tools.Utils;

    using NUnit.Framework;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Tests of where <see cref="TunnelRuns" /> cuts a curve and puts its mouths.
    ///     A level road crosses a ridge whose flanks rise one metre in two.
    ///     The flanks start at x = 200 and x = 600, so the cover is 12 m at x = 224 and x = 576.
    ///     A mouth goes out from there to 8.5 m, at x = 217 and x = 583.
    /// </summary>
    [TestFixture]
    public class TunnelRunsTests {
        /// <summary>
        ///     Height of the road and of the ground around the ridge.
        /// </summary>
        private const float Grade = 50f;

        /// <summary>
        ///     Position of the road across the ground.
        /// </summary>
        private const float Middle = TestTerrain.Depth / 2f;

        /// <summary>
        ///     Half the width of a small road.
        /// </summary>
        private const float HalfWidth = 8f;

        /// <summary>
        ///     Elevation limit of a road.
        /// </summary>
        private const float Limit = 4f;

        /// <summary>
        ///     Distance a mouth may be from where the formulas put it.
        ///     The search halves a step six times, and the heightmap rounds to 4 mm.
        /// </summary>
        private const float MouthTolerance = 0.1f;

        /// <summary>
        ///     Distance a mouth may be from where the formulas put it, on a road of uneven speed.
        ///     A step covers 4 m on the ground at the anchor, and the speed changes over the reach.
        /// </summary>
        private const float UnevenSpeedTolerance = 0.3f;

        [Test]
        public void Split_PlainHill_MouthsOutWhereTheCoverIsTwoLimitsAndAHalf() {
            NativeAllocations.Require();

            using (var terrain = new TestTerrain(Ridge)) {
                var road = Road(101.5f, 701.5f);
                var runs = Split(terrain, road);

                // The runs are the ground, an open cut, a tunnel, an open cut, and the ground.
                Assert.AreEqual(5, runs.Length);
                AssertWhole(runs);
                Assert.AreEqual(201.5f, X(road, runs[1].min), TunnelRuns.Step);
                Assert.AreEqual(217f, X(road, runs[2].min), MouthTolerance);
                Assert.AreEqual(583f, X(road, runs[2].max), MouthTolerance);
                Assert.AreEqual(601.5f, X(road, runs[3].max), TunnelRuns.Step);
            }
        }

        [Test]
        public void Split_SideSlope_MouthsStayWhereTheLowSideHasThreeLimits() {
            NativeAllocations.Require();

            // The ground drops 2 m from the centre to one edge: the ridge must make up for it.
            // Out from there, the head wall would show a step of 4 m less what the cap takes.
            using (var left = new TestTerrain((x, z) => Ridge(x, z) + (z - Middle) * 0.25f))
            using (var right = new TestTerrain((x, z) => Ridge(x, z) - (z - Middle) * 0.25f)) {
                var road = Road(101.5f, 701.5f);

                foreach (var terrain in new[] { left, right }) {
                    var runs = Split(terrain, road);

                    Assert.AreEqual(5, runs.Length);
                    AssertWhole(runs);
                    Assert.AreEqual(228f, X(road, runs[2].min), MouthTolerance);
                    Assert.AreEqual(572f, X(road, runs[2].max), MouthTolerance);
                }
            }
        }

        [Test]
        public void Split_DipUnderOneLimit_BreaksTheTunnel() {
            NativeAllocations.Require();

            // A ravine with 2 m of ground over the road for 20 m, its walls rising two in one.
            using (var terrain = new TestTerrain((x, z) => Ravine(x, z, 2f, 10f))) {
                var road = Road(101.5f, 701.5f);
                var runs = Split(terrain, road);

                // The ravine's cut lies between two tunnels, each with a cut to the ground.
                // The mouths have 12 m at 385 and 415, 8.5 m 1.75 m out on the walls.
                Assert.AreEqual(7, runs.Length);
                AssertWhole(runs);
                Assert.AreEqual(217f, X(road, runs[2].min), MouthTolerance);
                Assert.AreEqual(386.75f, X(road, runs[2].max), MouthTolerance);
                Assert.AreEqual(413.25f, X(road, runs[4].min), MouthTolerance);
                Assert.AreEqual(583f, X(road, runs[4].max), MouthTolerance);
            }
        }

        [Test]
        public void Split_DipToTheSurfaceShorterThanARun_StaysOneCut() {
            NativeAllocations.Require();

            // The ravine reaches the road for 8 m, under MinStretchLength: no run on the ground.
            using (var terrain = new TestTerrain((x, z) => Ravine(x, z, 0f, 4f))) {
                var road = Road(101.5f, 701.5f);
                var runs = Split(terrain, road);

                Assert.AreEqual(7, runs.Length);
                AssertWhole(runs);
                Assert.AreEqual(391.75f, X(road, runs[2].max), MouthTolerance);
                Assert.AreEqual(408.25f, X(road, runs[4].min), MouthTolerance);
            }
        }

        [Test]
        public void Split_DipUnderThreeLimitsAsLongAsTheWidth_BreaksTheTunnel() {
            NativeAllocations.Require();

            // A floor 20 m wide with 6 m then 10 m over the road: under 12 m for 26 and 22 m.
            // Over 6 m the piece is an open cut, and its mouths go out to 8.5 m.
            // Over 10 m the game makes a tunnel of it: its mouths stay at 12 m, or it would open.
            foreach (var floor in new[] { 6f, 10f }) {
                using (var terrain = new TestTerrain((x, z) => Ravine(x, z, floor, 10f))) {
                    var road  = Road(101.5f, 701.5f);
                    var runs  = Split(terrain, road);
                    var cover = floor < Limit * 2f ? Limit * 2f + TunnelRuns.Tolerance : Limit * 3f;
                    var reach = 10f + (cover - floor) * 0.5f;

                    // The ravine's piece lies between two tunnels, each with a cut to the ground.
                    Assert.AreEqual(7, runs.Length, $"{floor}");
                    AssertWhole(runs);
                    Assert.AreEqual(217f, X(road, runs[2].min), MouthTolerance, $"{floor}");
                    Assert.AreEqual(400f - reach, X(road, runs[2].max), MouthTolerance, $"{floor}");
                    Assert.AreEqual(400f + reach, X(road, runs[4].min), MouthTolerance, $"{floor}");
                    Assert.AreEqual(583f, X(road, runs[4].max), MouthTolerance, $"{floor}");
                }
            }
        }

        [Test]
        public void Split_NarrowDip_ItsMouthsKeepAPortalEdgeBetweenThem() {
            NativeAllocations.Require();

            // A notch down to the road, its walls rising four in one: 12 m at 396 and 404.
            // The 8.5 m points are closer than a portal edge: the mouths share the cut.
            // The tunnel after the dip starts a portal edge from the one before, at 405.5.
            using (var terrain = new TestTerrain((x, z) => Notch(x, z, 1f))) {
                var road = Road(101.5f, 701.5f);
                var runs = Split(terrain, road);

                Assert.AreEqual(7, runs.Length);
                AssertWhole(runs);
                Assert.AreEqual(396.75f, X(road, runs[2].max), MouthTolerance);
                Assert.AreEqual(404.75f, X(road, runs[4].min), MouthTolerance);
            }
        }

        [Test]
        public void Split_DipUnderThreeLimitsShorterThanTheWidth_StaysOneTunnel() {
            NativeAllocations.Require();

            // A ravine floor 4 m wide, with 6 m over the road: under 12 m for 10 m.
            // It lies at the tunnel's middle, where the game reads the cover between the mouths.
            // With both mouths out, no point it reads has three limits: the start goes back.
            using (var terrain = new TestTerrain((x, z) => Ravine(x, z, 6f, 2f))) {
                var road = Road(101.5f, 701.5f);
                var runs = Split(terrain, road);

                Assert.AreEqual(5, runs.Length);
                AssertWhole(runs);
                Assert.AreEqual(224f, X(road, runs[2].min), MouthTolerance);
                Assert.AreEqual(583f, X(road, runs[2].max), MouthTolerance);
            }
        }

        [Test]
        public void Split_GullyUnderThreeLimitsAtTheMiddle_OneMouthStaysAtThreeLimits() {
            NativeAllocations.Require();

            // A gully 2 m wide at x = 400, the tunnel's middle, 10 m then 12.2 m over the road.
            // With both mouths at 8.5 m the game reads three limits at the middle or nowhere.
            // A cover of 12.2 m is within a placed mouth's margin: the start goes back too.
            // Both mouths moved 7 m: the start goes back, and the end keeps its 8.5 m.
            foreach (var floor in new[] { 10f, 12.2f }) {
                using (var terrain = new TestTerrain((x, z) => Ravine(x, z, floor, 1f))) {
                    var road = Road(101.5f, 701.5f);
                    var runs = Split(terrain, road);

                    Assert.AreEqual(5, runs.Length, $"{floor}");
                    AssertWhole(runs);
                    Assert.AreEqual(224f, X(road, runs[2].min), MouthTolerance, $"{floor}");
                    Assert.AreEqual(583f, X(road, runs[2].max), MouthTolerance, $"{floor}");
                }
            }
        }

        [Test]
        public void Split_GullyBetweenUnequalFlanks_TheMouthThatMovedLessGoesBack() {
            NativeAllocations.Require();

            // The road climbs a flank of 1 in 2 and comes down a gentle one of 15 in 100.
            // The mouths go out 7 m to 217 and the whole reach to 532: the middle is at 374.5.
            // A gully with 10 m there: the start, which moved less, goes back to 224.
            using (var terrain = new TestTerrain(
                       (x, z) => GulliedRidge(x, z, 0.5f, 0.15f, 374.5f))) {
                var road = Road(101.5f, 701.5f);
                var runs = Split(terrain, road);

                Assert.AreEqual(5, runs.Length);
                AssertWhole(runs);
                Assert.AreEqual(224f, X(road, runs[2].min), MouthTolerance);
                Assert.AreEqual(532f, X(road, runs[2].max), MouthTolerance);
            }

            // On this road the middle is at 349, and the end moved less along the curve.
            // The moves are compared on the ground: the start goes back all the same.
            using (var terrain = new TestTerrain(
                       (x, z) => GulliedRidge(x, z, 0.5f, 0.15f, 349f))) {
                var road = RoadOfUnevenSpeed();
                var runs = Split(terrain, road);

                Assert.AreEqual(5, runs.Length);
                AssertWhole(runs);
                Assert.AreEqual(224f, X(road, runs[2].min), MouthTolerance);
                Assert.AreEqual(532f, X(road, runs[2].max), UnevenSpeedTolerance);
            }
        }

        [Test]
        public void Split_GullyWithAMovedMouthNearlyAtThreeLimits_TheMouthThatMovedLessGoesBack() {
            NativeAllocations.Require();

            // A track reaches 4 m out: down the flank of 1 in 10, its start keeps 11.6 m at 316.
            // Its end goes 3.5 m down the flank of 1 in 1, to 8.5 m at 591.5.
            // The game reads three limits at no mouth that moved, nor at the gully in the middle.
            // The end, which moved less, goes back to 12 m at 588.
            using (var terrain = new TestTerrain((x, z) => GulliedRidge(x, z, 0.1f, 1f, 454f))) {
                var road = Road(101.5f, 701.5f);
                var runs = Split(terrain, road, 4f);

                Assert.AreEqual(5, runs.Length);
                AssertWhole(runs);
                Assert.AreEqual(316f, X(road, runs[2].min), MouthTolerance);
                Assert.AreEqual(588f, X(road, runs[2].max), MouthTolerance);
            }
        }

        [Test]
        public void Split_GentleSlope_MouthsGoOutAsFarAsTheReach() {
            NativeAllocations.Require();

            // Flanks of 15 in 100: 12 m at 280 and 520, 8.5 m more than 12 m further out.
            using (var terrain = new TestTerrain(GentleRidge)) {
                var road = Road(101.5f, 701.5f);
                var runs = Split(terrain, road);

                Assert.AreEqual(5, runs.Length);
                AssertWhole(runs);
                Assert.AreEqual(268f, X(road, runs[2].min), MouthTolerance);
                Assert.AreEqual(532f, X(road, runs[2].max), MouthTolerance);
            }
        }

        [Test]
        public void Split_CurveRunningFastOrSlowAtAMouth_MouthGoesOutTheReachInMetres() {
            NativeAllocations.Require();

            // The road runs slow at 280 and fast at 520: the mouths still go out 12 m.
            using (var terrain = new TestTerrain(GentleRidge)) {
                var road = RoadOfUnevenSpeed();
                var runs = Split(terrain, road);

                Assert.AreEqual(5, runs.Length);
                AssertWhole(runs);
                Assert.AreEqual(268f, X(road, runs[2].min), UnevenSpeedTolerance);
                Assert.AreEqual(532f, X(road, runs[2].max), UnevenSpeedTolerance);
            }
        }

        [Test]
        public void Split_Track_MouthsGoOutOneStep() {
            NativeAllocations.Require();

            // A track 8 m wide reaches 4 m out from 12 m: 10 m of cover on the ridge.
            using (var terrain = new TestTerrain(Ridge)) {
                var road = Road(101.5f, 701.5f);
                var runs = Split(terrain, road, 4f);

                Assert.AreEqual(5, runs.Length);
                AssertWhole(runs);
                Assert.AreEqual(220f, X(road, runs[2].min), MouthTolerance);
                Assert.AreEqual(580f, X(road, runs[2].max), MouthTolerance);
            }
        }

        [Test]
        public void Split_WideRoad_MouthsStopAtTwoLimitsAndAHalfWithinTheReach() {
            NativeAllocations.Require();

            // A road 32 m wide reaches 28 m out: on flanks of 15 in 100, 8.5 m comes first.
            using (var terrain = new TestTerrain(GentleRidge)) {
                var road = Road(101.5f, 701.5f);
                var runs = Split(terrain, road, 16f);

                Assert.AreEqual(5, runs.Length);
                AssertWhole(runs);
                Assert.AreEqual(200f + 8.5f / 0.15f, X(road, runs[2].min), MouthTolerance);
                Assert.AreEqual(600f - 8.5f / 0.15f, X(road, runs[2].max), MouthTolerance);
            }
        }

        [Test]
        public void Split_BumpOnOneSide_PassedOver() {
            NativeAllocations.Require();

            // A head wall at x = 220 would show a step: the point at 8.5 m beyond it is level.
            using (var terrain = new TestTerrain(BumpedRidge)) {
                var road = Road(101.5f, 701.5f);
                var runs = Split(terrain, road);

                Assert.AreEqual(5, runs.Length);
                AssertWhole(runs);
                Assert.AreEqual(217f, X(road, runs[2].min), MouthTolerance);
                Assert.AreEqual(583f, X(road, runs[2].max), MouthTolerance);
            }
        }

        [Test]
        public void Split_CurveEndingInsideTheHill_TunnelRunsToTheEnd() {
            NativeAllocations.Require();

            using (var terrain = new TestTerrain(Ridge)) {
                var road = Road(101.5f, 400f);
                var runs = Split(terrain, road);

                Assert.AreEqual(3, runs.Length);
                AssertWhole(runs);
                Assert.AreEqual(217f, X(road, runs[2].min), MouthTolerance);
            }
        }

        [Test]
        public void Split_ShortCurveWithOneEndInsideTheHill_TunnelOnceItsMouthMovesOut() {
            NativeAllocations.Require();

            using (var terrain = new TestTerrain(Ridge)) {
                // One end has 12.5 m: only the sample there has three limits, whichever way.
                // From 12 m at x = 224 the tunnel is too short, from 8.5 m at x = 217 it is not.
                var forward  = Road(208f, 225f);
                var backward = Road(225f, 208f);
                var ahead    = Split(terrain, forward);
                var behind   = Split(terrain, backward);

                Assert.AreEqual(2, ahead.Length);
                AssertWhole(ahead);
                Assert.AreEqual(217f, X(forward, ahead[0].max), MouthTolerance);
                Assert.AreEqual(2, behind.Length);
                AssertWhole(behind);
                Assert.AreEqual(217f, X(backward, behind[0].max), MouthTolerance);
            }
        }

        [Test]
        public void Split_EitherEndWithTheCoverOfAMouth_TunnelRunsToTheNode() {
            NativeAllocations.Require();

            using (var terrain = new TestTerrain(Ridge)) {
                // The node has 8.25 m of cover: a node is allowed the tolerance.
                // No cut is left for nothing between the node and a mouth, whichever way.
                var forward  = Road(216.5f, 701.5f);
                var backward = Road(701.5f, 216.5f);
                var ahead    = Split(terrain, forward);
                var behind   = Split(terrain, backward);

                Assert.AreEqual(3, ahead.Length);
                AssertWhole(ahead);
                Assert.AreEqual(583f, X(forward, ahead[0].max), MouthTolerance);
                Assert.AreEqual(3, behind.Length);
                AssertWhole(behind);
                Assert.AreEqual(583f, X(backward, behind[2].min), MouthTolerance);
            }
        }

        [Test]
        public void Split_EitherEndShortOfTheCover_MouthAPortalEdgeIn() {
            NativeAllocations.Require();

            using (var terrain = new TestTerrain(Ridge)) {
                // The node has 7 m of cover: it cannot be the mouth.
                // The mouth would go out to 8.5 m, 3 m from the node: it keeps a portal edge.
                var forward  = Road(214f, 701.5f);
                var backward = Road(701.5f, 214f);
                var ahead    = Split(terrain, forward);
                var behind   = Split(terrain, backward);
                var step     = 1f / (TunnelRuns.SampleCount(MathUtils.Length(forward)) - 1);
                var portal   = TunnelRuns.MinPortalSamples * step;

                Assert.AreEqual(4, ahead.Length);
                AssertWhole(ahead);
                Assert.AreEqual(portal, ahead[0].max, 0.0001f);
                Assert.AreEqual(583f, X(forward, ahead[1].max), MouthTolerance);
                Assert.AreEqual(4, behind.Length);
                AssertWhole(behind);
                Assert.AreEqual(583f, X(backward, behind[2].min), MouthTolerance);
                Assert.AreEqual(1f - portal, behind[2].max, 0.0001f);
            }
        }

        [Test]
        public void Split_FirstRunShorterThanARun_JoinsTheCut() {
            NativeAllocations.Require();

            using (var terrain = new TestTerrain(Ridge)) {
                // The road runs 6 m on the ground before the ridge: too short for an edge.
                var road = Road(194f, 701.5f);
                var runs = Split(terrain, road);

                // The runs are a cut, a tunnel, a cut, and the ground.
                Assert.AreEqual(4, runs.Length);
                AssertWhole(runs);
                Assert.AreEqual(217f, X(road, runs[0].max), MouthTolerance);
            }
        }

        [Test]
        public void Split_FlatGround_OneRun() {
            NativeAllocations.Require();

            using (var terrain = new TestTerrain((x, z) => Grade)) {
                var runs = Split(terrain, Road(100f, 300f));

                Assert.AreEqual(1, runs.Length);
                AssertWhole(runs);
            }
        }

        [Test]
        public void Split_EachRunSplitAgain_ComesBackAsOneRun() {
            NativeAllocations.Require();

            // A second apply of the Slope tool splits the pieces of the first again.
            // On the ridge and on a ravine wider than a sample, each piece comes back whole.
            using (var ridge = new TestTerrain(Ridge))
            using (var ravine = new TestTerrain((x, z) => Ravine(x, z, 2f, 10f))) {
                var road = Road(101.5f, 701.5f);

                foreach (var terrain in new[] { ridge, ravine }) {
                    foreach (var run in Split(terrain, road)) {
                        var again = Split(terrain, MathUtils.Cut(road, run));

                        Assert.AreEqual(1, again.Length, $"{run.min} to {run.max}");
                    }
                }
            }
        }

        [Test]
        public void Split_AnyStartAndEnd_RunsAreWholeAndEveryMouthCanBeOne() {
            NativeAllocations.Require();

            using (var terrain = new TestTerrain(Ridge)) {
                for (var start = 100f; start < 104f; start += 0.37f) {
                    for (var end = 298f; end < 706f; end += 50.9f) {
                        var road = Road(start, end);
                        var runs = Split(terrain, road);

                        AssertWhole(runs);
                        AssertMouths(terrain, road, runs);
                    }
                }
            }
        }

        [Test]
        public void SplitAtGrade_PlainHill_OneCourseForEachRunWithNewNodesAtTheCuts() {
            NativeAllocations.Require();

            using (var terrain = new TestTerrain(Ridge)) {
                var road  = Road(101.5f, 701.5f);
                var start = new Entity { Index = 1, Version = 1 };
                var end   = new Entity { Index = 2, Version = 1 };
                var split = new NativeList<EdgeConfig>(8, Allocator.Temp);

                var curve = new EdgeConfig {
                    Bezier          = road,
                    Length          = MathUtils.Length(road),
                    StartNodeEntity = start,
                    EndNodeEntity   = end,
                };

                TunnelRuns.SplitAtGrade(
                    ref terrain.Data,
                    curve,
                    HalfWidth,
                    Limit,
                    CoursePosFlags.FreeHeight,
                    ref split);

                Assert.AreEqual(5, split.Length);
                Assert.AreEqual(start, split[0].StartNodeEntity);
                Assert.AreEqual(end, split[4].EndNodeEntity);

                var length = 0f;

                for (var i = 0; i < split.Length; i++) {
                    length += split[i].Length;

                    // One end at the limit and one at minus the limit keep a course on its line.
                    Assert.AreEqual(Limit, split[i].StartNodeElevation);
                    Assert.AreEqual(-Limit, split[i].EndNodeElevation);

                    if (i > 0) {
                        Assert.AreEqual(Entity.Null, split[i].StartNodeEntity);
                        Assert.AreEqual(CoursePosFlags.FreeHeight, split[i].StartNodeFlags);
                        Assert.AreEqual(split[i - 1].Bezier.d.x, split[i].Bezier.a.x, 0.001f);
                    }
                }

                Assert.AreEqual(curve.Length, length, 0.01f);
                split.Dispose();
            }
        }

        [Test]
        public void Cover_SideSlope_IsTheLeastOverTheWidth() {
            // The centre has 20 m over it, the left 2 m more, the right 2 m less.
            using (var terrain = new TestTerrain((x, z) => Grade + 20f + (z - Middle) * 0.25f)) {
                var road      = Road(100f, 300f);
                var cover     = TunnelRuns.Cover(ref terrain.Data, road, 0.5f, HalfWidth);
                var elevation = TunnelRuns.Elevation(ref terrain.Data, road, 0.5f, HalfWidth);

                Assert.AreEqual(18f, cover, 0.01f);
                Assert.AreEqual(-22f, elevation.x, 0.01f);
                Assert.AreEqual(-18f, elevation.y, 0.01f);
            }
        }

        [Test]
        public void Cover_RoadAboveTheGround_IsNegative() {
            using (var terrain = new TestTerrain((x, z) => Grade - 5f)) {
                var cover = TunnelRuns.Cover(ref terrain.Data, Road(100f, 300f), 0.5f, HalfWidth);

                Assert.AreEqual(-5f, cover, 0.01f);
            }
        }

        [Test]
        public void FirstMouth_FromEitherEnd_FindsTheNearestMouth() {
            using (var terrain = new TestTerrain(Ridge)) {
                var road     = Road(101.5f, 701.5f);
                var forward  = FirstMouth(terrain, road, 0f, 1f);
                var backward = FirstMouth(terrain, road, 1f, 0f);

                Assert.AreEqual(217f, X(road, forward), MouthTolerance);
                Assert.AreEqual(583f, X(road, backward), MouthTolerance);
            }
        }

        [Test]
        public void FirstMouth_NodeWithinReach_StaysOrSlidesToTwoLimitsAndAHalf() {
            using (var terrain = new TestTerrain(Ridge)) {
                var road   = Road(101.5f, 701.5f);
                var length = MathUtils.Length(road);

                // The cover is 9 m at x = 218, enough for a node, and 8.2 m at 216.4, too little.
                var enough  = (218f - 101.5f) / length;
                var shallow = (216.4f - 101.5f) / length;

                var stays  = FirstMouth(terrain, road, enough, 1f);
                var slides = FirstMouth(terrain, road, shallow, 1f);

                Assert.AreEqual(enough, stays);
                Assert.AreEqual(217f, X(road, slides), MouthTolerance);
            }
        }

        [Test]
        public void FirstMouth_StartDeepEnough_IsTheStart() {
            using (var terrain = new TestTerrain(Ridge)) {
                var road  = Road(101.5f, 701.5f);
                var found = FirstMouth(terrain, road, 0.5f, 1f);

                Assert.AreEqual(0.5f, found);
            }
        }

        [Test]
        public void FirstMouth_NoPointDeepEnough_IsNegative() {
            using (var terrain = new TestTerrain(Ridge)) {
                var road = Road(101.5f, 701.5f);

                // The search stops at x = 221.5, where the cover is 10.75 m.
                var found = FirstMouth(terrain, road, 0f, 0.2f);

                Assert.Less(found, 0f);
            }
        }

        [Test]
        public void Stored_UnderTheGround_IsTheMeasuredElevation() {
            var stored = TunnelRuns.Stored(new float2(-13f, -4f), new float2(6f, 6f), Limit);

            Assert.AreEqual(new float2(-13f, -4f), stored);
        }

        [Test]
        public void Stored_WithinTheLimit_IsNone() {
            var stored = TunnelRuns.Stored(new float2(-3.9f, 3.9f), new float2(6f, -6f), Limit);

            Assert.AreEqual(new float2(0f, 0f), stored);
        }

        [Test]
        public void Stored_AboveTheLimit_KeepsABridgeAndMakesNone() {
            var stored = TunnelRuns.Stored(new float2(5f, 5f), new float2(6f, -8f), Limit);

            Assert.AreEqual(new float2(6f, 0f), stored);
        }

        [Test]
        public void IsMouth_Placed_NeedsTwoLimitsAndAHalfAndALevelHeadWall() {
            Assert.IsTrue(IsMouth(8.55f, 0f, 0f));
            Assert.IsFalse(IsMouth(8.45f, 0f, 0f));
            Assert.IsTrue(IsMouth(9f, 0.45f, 0f));
            Assert.IsFalse(IsMouth(9f, 0.6f, 0f));
        }

        [Test]
        public void IsMouth_Judged_AllowedTheTolerance() {
            Assert.IsTrue(IsMouth(8.05f, 0f, TunnelRuns.Tolerance));
            Assert.IsFalse(IsMouth(7.95f, 0f, TunnelRuns.Tolerance));
            Assert.IsTrue(IsMouth(9f, 0.95f, TunnelRuns.Tolerance));
            Assert.IsFalse(IsMouth(9f, 1.1f, TunnelRuns.Tolerance));
        }

        [Test]
        public void IsMouth_SlantAboveThreeLimits_IsLevelOnceCapped() {
            // The low side has 14 m and the high one 18 m: the game caps both at 12 m.
            Assert.IsTrue(IsMouth(14f, 4f, 0f));

            // With 10 m and 14 m, the high side capped at 12 m still leaves a step of 2 m.
            Assert.IsFalse(IsMouth(10f, 4f, TunnelRuns.Tolerance));
        }

        [Test]
        public void Reach_SmallRoadAndTrack_TheWidthLessAStep() {
            Assert.AreEqual(12f, TunnelRuns.Reach(HalfWidth));
            Assert.AreEqual(4f, TunnelRuns.Reach(4f));
        }

        [Test]
        public void SampleCount_AnyLength_CoversBothEndsEveryStep() {
            Assert.AreEqual(2, TunnelRuns.SampleCount(0f));
            Assert.AreEqual(2, TunnelRuns.SampleCount(4f));
            Assert.AreEqual(3, TunnelRuns.SampleCount(4.1f));
            Assert.AreEqual(151, TunnelRuns.SampleCount(600f));
        }

        [Test]
        public void CanTunnel_ElevationRangeBelowZero_IsTrue() {
            var road = new PlaceableNetData {
                m_ElevationRange = new Bounds1(-50f, 50f),
            };

            var quay = new PlaceableNetData {
                m_ElevationRange = new Bounds1(0f, 0f),
            };

            Assert.IsTrue(TunnelRuns.CanTunnel(road));
            Assert.IsFalse(TunnelRuns.CanTunnel(quay));
        }

        /// <summary>
        ///     Gets the height of a ridge across the road, with a flat top 40 m over it.
        /// </summary>
        /// <param name="x">Position along the road.</param>
        /// <param name="z">Position across the road.</param>
        /// <returns>The height of the ground.</returns>
        private static float Ridge(float x, float z) {
            return Grade + math.clamp(math.min(x - 200f, 600f - x) * 0.5f, 0f, 40f);
        }

        /// <summary>
        ///     Gets the height of a ridge across the road whose flanks rise 15 in 100.
        /// </summary>
        /// <param name="x">Position along the road.</param>
        /// <param name="z">Position across the road.</param>
        /// <returns>The height of the ground.</returns>
        private static float GentleRidge(float x, float z) {
            return Grade + math.clamp(math.min(x - 200f, 600f - x) * 0.15f, 0f, 40f);
        }

        /// <summary>
        ///     Gets the height of the ridge with the ground 2 m higher on one side around x = 220.
        /// </summary>
        /// <param name="x">Position along the road.</param>
        /// <param name="z">Position across the road.</param>
        /// <returns>The height of the ground.</returns>
        private static float BumpedRidge(float x, float z) {
            return Ridge(x, z) + (z > Middle && math.abs(x - 220f) <= 2f ? 2f : 0f);
        }

        /// <summary>
        ///     Gets the height of the ridge with a notch down to the road at x = 400.
        ///     Its walls rise four in one.
        /// </summary>
        /// <param name="x">Position along the road.</param>
        /// <param name="z">Position across the road.</param>
        /// <param name="halfFloor">Half the width of the bottom.</param>
        /// <returns>The height of the ground.</returns>
        private static float Notch(float x, float z, float halfFloor) {
            var wall = Grade + math.max(0f, math.abs(x - 400f) - halfFloor) * 4f;

            return math.min(Ridge(x, z), wall);
        }

        /// <summary>
        ///     Gets the height of the ridge with a ravine across the road at x = 400.
        /// </summary>
        /// <param name="x">Position along the road.</param>
        /// <param name="z">Position across the road.</param>
        /// <param name="floor">Ground over the road at the bottom of the ravine.</param>
        /// <param name="halfFloor">Half the width of the bottom.</param>
        /// <returns>The height of the ground.</returns>
        private static float Ravine(float x, float z, float floor, float halfFloor) {
            var wall = Grade + floor + math.max(0f, math.abs(x - 400f) - halfFloor) * 2f;

            return math.min(Ridge(x, z), wall);
        }

        /// <summary>
        ///     Gets the height of a ridge with flanks of their own and a gully across its top.
        ///     The flanks rise from x = 200 and x = 600, the ridge capped at 40 m over the road.
        ///     The gully is 2 m wide with 10 m over the road, its walls rising two in one.
        /// </summary>
        /// <param name="x">Position along the road.</param>
        /// <param name="z">Position across the road.</param>
        /// <param name="climb">Slope of the flank the road climbs from x = 200.</param>
        /// <param name="descent">Slope of the flank the road comes down to x = 600.</param>
        /// <param name="gullyX">Position of the gully along the road.</param>
        /// <returns>The height of the ground.</returns>
        private static float GulliedRidge(
            float x,
            float z,
            float climb,
            float descent,
            float gullyX) {
            var flanks = math.min((x - 200f) * climb, (600f - x) * descent);
            var ridge  = Grade + math.clamp(flanks, 0f, 40f);
            var wall   = Grade + 10f + math.max(0f, math.abs(x - gullyX) - 1f) * 2f;

            return math.min(ridge, wall);
        }

        /// <summary>
        ///     Makes a straight, level road along x in the middle of the ground.
        /// </summary>
        /// <param name="from">Where the road starts.</param>
        /// <param name="to">Where the road ends.</param>
        /// <returns>The curve of the road.</returns>
        private static Bezier4x3 Road(float from, float to) {
            var a = new float3(from, Grade, Middle);
            var d = new float3(to, Grade, Middle);

            return NetUtils.StraightCurve(a, d);
        }

        /// <summary>
        ///     Makes the road from x = 101.5 to 701.5 with its handles at three and four tenths.
        ///     It runs slower than on average where x is 280, and faster where x is 520.
        /// </summary>
        /// <returns>The curve of the road.</returns>
        private static Bezier4x3 RoadOfUnevenSpeed() {
            var road = Road(101.5f, 701.5f);

            road.b = math.lerp(road.a, road.d, 0.3f);
            road.c = math.lerp(road.a, road.d, 0.4f);

            return road;
        }

        /// <summary>
        ///     Gets where a curve position lies along x.
        /// </summary>
        /// <param name="road">The curve.</param>
        /// <param name="t">Curve position.</param>
        /// <returns>The x of the point.</returns>
        private static float X(Bezier4x3 road, float t) {
            return MathUtils.Position(road, t).x;
        }

        /// <summary>
        ///     Checks whether a node in the middle of a road on even ground can be a mouth.
        ///     The ground is level along the road, and slants across it.
        /// </summary>
        /// <param name="low">Ground over the road's lower edge.</param>
        /// <param name="rise">How much higher the ground is over the other edge.</param>
        /// <param name="slack">What <see cref="TunnelRuns.IsMouth" /> allows.</param>
        /// <returns>True if a node there can be a mouth.</returns>
        private static bool IsMouth(float low, float rise, float slack) {
            var slope = rise / (HalfWidth * 2f);

            using (var terrain = new TestTerrain(
                       (x, z) => Grade + low + rise * 0.5f + (z - Middle) * slope)) {
                var road = Road(100f, 300f);

                return TunnelRuns.IsMouth(ref terrain.Data, road, 0.5f, HalfWidth, Limit, slack);
            }
        }

        /// <summary>
        ///     Splits a road and copies the runs out of their native list.
        /// </summary>
        /// <param name="terrain">The ground.</param>
        /// <param name="road">The curve to split.</param>
        /// <param name="halfWidth">Half the network's width, a small road's by default.</param>
        /// <returns>The runs, in order.</returns>
        private static Bounds1[] Split(
            TestTerrain terrain,
            Bezier4x3   road,
            float       halfWidth = HalfWidth) {
            var runs = new NativeList<Bounds1>(8, Allocator.Temp);

            TunnelRuns.Split(
                ref terrain.Data,
                road,
                MathUtils.Length(road),
                halfWidth,
                Limit,
                ref runs);

            var result = new Bounds1[runs.Length];

            for (var i = 0; i < result.Length; i++) {
                result[i] = runs[i];
            }

            runs.Dispose();

            return result;
        }

        /// <summary>
        ///     Finds the first point that can be a mouth, going along a small road.
        /// </summary>
        /// <param name="terrain">The ground.</param>
        /// <param name="road">The curve.</param>
        /// <param name="from">Curve position to search from.</param>
        /// <param name="to">Curve position to search up to.</param>
        /// <returns>The curve position of the mouth, negative when there is none.</returns>
        private static float FirstMouth(TestTerrain terrain, Bezier4x3 road, float from, float to) {
            return TunnelRuns.FirstMouth(
                ref terrain.Data,
                road,
                MathUtils.Length(road),
                HalfWidth,
                Limit,
                from,
                to);
        }

        /// <summary>
        ///     Checks what holds for any split: the runs cover the curve, in order, none empty.
        ///     An empty run makes an edge of no length, which the game refuses.
        /// </summary>
        /// <param name="runs">The runs to check.</param>
        private static void AssertWhole(Bounds1[] runs) {
            Assert.GreaterOrEqual(runs.Length, 1);
            Assert.AreEqual(0f, runs[0].min);
            Assert.AreEqual(1f, runs[runs.Length - 1].max);

            for (var i = 0; i < runs.Length; i++) {
                Assert.Greater(MathUtils.Size(runs[i]), 0.001f, $"run {i} is empty");

                if (i > 0) {
                    Assert.AreEqual(runs[i - 1].max, runs[i].min, $"run {i} leaves a gap");
                }
            }
        }

        /// <summary>
        ///     Checks that each tunnel of a split starts and ends where a mouth can be.
        ///     A mouth placed on the ridge goes out to 8.5 m of cover, level across.
        ///     An end of the curve is a node already, allowed the tolerance.
        /// </summary>
        /// <param name="terrain">The ground.</param>
        /// <param name="road">The curve that was split.</param>
        /// <param name="runs">The runs to check.</param>
        private static void AssertMouths(TestTerrain terrain, Bezier4x3 road, Bounds1[] runs) {
            var placed = Limit * 2f + TunnelRuns.Tolerance;

            for (var i = 0; i < runs.Length; i++) {
                var middle = MathUtils.Center(runs[i]);

                if (TunnelRuns.Cover(ref terrain.Data, road, middle, HalfWidth) < Limit * 3f) {
                    continue;
                }

                foreach (var end in new[] { runs[i].min, runs[i].max }) {
                    var node  = end <= 0f || end >= 1f;
                    var slack = node ? TunnelRuns.Tolerance : 0f;
                    var cover = TunnelRuns.Cover(ref terrain.Data, road, end, HalfWidth);

                    Assert.IsTrue(
                        TunnelRuns.IsMouth(ref terrain.Data, road, end, HalfWidth, Limit, slack),
                        $"run {i} at {end}");

                    if (!node) {
                        Assert.AreEqual(placed, cover, MouthTolerance, $"run {i} at {end}");
                    }
                }
            }
        }
    }
}
