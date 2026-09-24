namespace NetworkTools.Tests {
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Colossal.Mathematics;
    using Colossal.Reflection;
    using Colossal.Serialization.Entities;
    using Colossal.TestFramework;

    using Game;
    using Game.Areas;
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.SceneFlow;
    using Game.Simulation;

    using NetworkTools.Systems.Tools.Connect;
    using NetworkTools.Systems.Tools.Parallel;
    using NetworkTools.Systems.Tools.RoadShape;

    using Unity.Collections;
    using Unity.Mathematics;

    using UnityEngine;

    using Lane = NetworkTools.Tests.ProvingGround.Lane;
    using Mouth = NetworkTools.Tests.NetworkReader.Mouth;

    /// <summary>
    ///     Lays networks across the <see cref="ProvingGround" /> with each tool, Tunnel mode on.
    ///     Checks that the mouths previewed and built are where the terrain's formulas put them.
    ///     The scenario opens the editor on an empty map: it needs no map and no save.
    ///     Each test lays its roads at its own distance from the middle of a lane.
    ///     On the dome, each lays its own bend.
    ///     Its category is one no scenario of the game carries.
    ///     The game started with <c>--categoryFilter=QA</c> runs it alone, with nothing attached.
    /// </summary>
    [TestDescriptor("NetworkTools: Tunnel mode", Category.QA)]
    public class TunnelModeTest : TestScenario {
        /// <summary>
        ///     Distance a mouth may be from where the formulas put it.
        ///     The search for a mouth stops within 7 cm of it.
        /// </summary>
        private const float Tolerance = 0.15f;

        /// <summary>
        ///     Distance a node may be from where it was, to count as not moved.
        /// </summary>
        private const float Still = 0.01f;

        /// <summary>
        ///     Most frames to wait for the Slope tool's preview to hold still.
        /// </summary>
        private const int Previewed = 30;

        /// <summary>
        ///     Most frames to wait for the Slope tool's apply, which runs over three frames.
        /// </summary>
        private const int Applied = 45;

        /// <summary>
        ///     Frames the edges must hold still, once changed, for a wait to end early.
        /// </summary>
        private const int Quiet = 5;

        /// <summary>
        ///     Room left around the proving ground in the camera's view, as a factor of its size.
        /// </summary>
        private const float ViewMargin = 1.05f;

        /// <summary>
        ///     Where the roads of a lane start, on the flat ground before its relief.
        /// </summary>
        private const float Start = 41f;

        /// <summary>
        ///     Where the roads of a lane end, on the flat ground after its relief.
        /// </summary>
        private const float End = 1011f;

        /// <summary>
        ///     Distance a mouth cut into a road may be from where the formulas put it.
        ///     The game puts a new node of a road on a multiple of 4 m along the edge it cuts.
        /// </summary>
        private const float CutTolerance = 2f + Tolerance;

        /// <summary>
        ///     Ground cover of a mouth of a road that stays where the whole width has three limits.
        ///     On a bend through the dome, the ground slants across the road under that.
        /// </summary>
        private const float Deep = 12f;

        /// <summary>
        ///     Ground cover of a mouth of a road moved out on ground level across it.
        /// </summary>
        private const float Shallow = 8.5f;

        /// <summary>
        ///     Cover a mouth may have over or under the one expected, when read on a bend.
        ///     A flank rising one in two moves it by twice that along the road.
        /// </summary>
        private const float CoverTolerance = 0.1f;

        /// <summary>
        ///     The same for a mouth cut into a road, which the game moves by up to 2 m.
        /// </summary>
        private const float CutCoverTolerance = 1.1f;

        /// <summary>
        ///     Half the width of a small road, the network of the bends.
        /// </summary>
        private const float HalfWidth = 8f;

        /// <summary>
        ///     Half the width of a track, 8 m wide: its mouths go out by one step at most.
        /// </summary>
        private const float TrackHalfWidth = 4f;

        /// <summary>
        ///     Distance around the bounds of a bend within which its mouths and its copies' lie.
        ///     The other bends on the dome have none there.
        /// </summary>
        private const float BendMargin = 40f;

        /// <summary>
        ///     Depth of the sag the arch case gives the middle of its road.
        /// </summary>
        private const float ArchSag = 6f;

        /// <summary>
        ///     Where the ground roads to slope are laid in two pieces, and get their side road.
        ///     On <see cref="Lane.Gentle" /> that is 10 m before the point with 12 m, under 10.5 m.
        /// </summary>
        private const float JointAlong = 210f;

        /// <summary>
        ///     Where the game itself puts a node on a ground road up a flank of one in two.
        ///     On <see cref="Lane.Deep" /> that is 4 m before the first mouth, too near for a cut.
        /// </summary>
        private const float OwnNodeAlong = 153f;

        /// <summary>
        ///     Name of the road prefab most cases lay.
        /// </summary>
        private const string SmallRoad = "Small Road";

        /// <summary>
        ///     Name of the track prefab.
        /// </summary>
        private const string TrainTrack = "Twoway Train Track";

        /// <summary>
        ///     Name of the path prefab.
        /// </summary>
        private const string PavementPath = "Pavement Path";

        private NT_ConnectToolSystem   m_ConnectTool;
        private NT_RoadShapeToolSystem m_SlopeTool;
        private NT_ParallelToolSystem  m_ParallelTool;

        /// <summary>
        ///     The tools' parameters the user saved, put back when the scenario ends.
        /// </summary>
        private string m_SavedParameters;

        /// <summary>
        ///     Checks passed and failed by the test under way.
        /// </summary>
        private int m_Passed;
        private int m_Failed;

        /// <summary>
        ///     True while the checks under way are known to come out other than asked for.
        ///     They are reported and do not fail the test.
        /// </summary>
        private bool m_Known;

        /// <summary>
        ///     Names of the tests, in the order the runner runs them.
        ///     Then the index of the one under way, from one.
        /// </summary>
        private string[] m_Tests;
        private int      m_Test;

        /// <summary>
        ///     The progress shown on screen.
        /// </summary>
        private ProgressOverlay m_Overlay;

        /// <summary>
        ///     A ground road along a lane made level with the Slope tool, Tunnel on.
        /// </summary>
        private sealed class SlopeCase {
            /// <summary>
            ///     Name of the case, for the log.
            /// </summary>
            public string Name;

            /// <summary>
            ///     The lane.
            /// </summary>
            public Lane Lane;

            /// <summary>
            ///     Distance of the road to the left of the lane's middle.
            /// </summary>
            public float Across;

            /// <summary>
            ///     Type of the network prefab.
            /// </summary>
            public string Type = nameof(RoadPrefab);

            /// <summary>
            ///     Name of the network prefab.
            /// </summary>
            public string Network = SmallRoad;

            /// <summary>
            ///     Where the two ground pieces meet, and the side road starts.
            /// </summary>
            public float Joint = JointAlong;

            /// <summary>
            ///     Where a third ground piece starts, with a side road of its own, or not a number.
            /// </summary>
            public float SecondJoint = float.NaN;

            /// <summary>
            ///     Distance to the left of the lane's middle where the side road ends.
            ///     Not a number for no side road.
            /// </summary>
            public float SideRoadTo = float.NaN;

            /// <summary>
            ///     True when the side road is cut at a mouth of its own.
            ///     Its cover is <see cref="Shallow" />.
            /// </summary>
            public bool SideCut;

            /// <summary>
            ///     Where the ground road must have a node once laid, or not a number.
            /// </summary>
            public float LaidNode = float.NaN;

            /// <summary>
            ///     True to lay the road at the height of the flat ground, through the relief.
            ///     The game then tunnels it by its own rule, and the case cuts that again.
            /// </summary>
            public bool Level;

            /// <summary>
            ///     True to make the road level with Tunnel off first.
            /// </summary>
            public bool LevelFirst;

            /// <summary>
            ///     True when the two applies build different nodes: that is reported, not failed.
            /// </summary>
            public bool KnownFirstApply;

            /// <summary>
            ///     True to select the path from its end to its start.
            /// </summary>
            public bool Reversed;

            /// <summary>
            ///     The slope template, with the sag of <see cref="Sag" /> for the arch.
            /// </summary>
            public ShapeTransformTemplate Template = ShapeTransformTemplate.SlopeLinear;

            /// <summary>
            ///     Depth of the sag the arch gives the middle of the road.
            /// </summary>
            public float Sag = ArchSag;

            /// <summary>
            ///     Distances along the lane, two for each tunnel.
            /// </summary>
            public float[] Expected;

            /// <summary>
            ///     Where a node must be afterwards, or not a number.
            /// </summary>
            public float Node = float.NaN;

            /// <summary>
            ///     Distance a mouth cut into the road may be from where the formulas put it.
            /// </summary>
            public float CutWithin = CutTolerance;
        }

        /// <inheritdoc />
        protected override async Task OnPrepare() {
            // The load must start from the game's own loop, not from whatever called the scenario.
            await WaitFrames(2);

            m_ConnectTool  = World.GetOrCreateSystemManaged<NT_ConnectToolSystem>();
            m_SlopeTool    = World.GetOrCreateSystemManaged<NT_RoadShapeToolSystem>();
            m_ParallelTool = World.GetOrCreateSystemManaged<NT_ParallelToolSystem>();

            // A tool starts with the parameters saved last: the scenario starts with none.
            var settings = NetworkToolsMod.Instance.Settings;

            m_SavedParameters             = settings.SavedParameterValues;
            settings.SavedParameterValues = "{}";

            await GameManager.instance.Load(GameMode.Editor, Purpose.NewMap);
            await WaitFrames(60);

            ProvingGroundLoader.Apply(World);
            await WaitFrames(60);

            LookDown();
            ClearStartTiles();

            m_Tests = System.Linq.Enumerable.ToArray(
                System.Linq.Enumerable.Select(
                    GetType().GetMethodsWithAttributeAndSignature<TestAttribute>(
                        typeof(void),
                        typeof(Task)),
                    method => method.Name));

            m_Overlay = ProgressOverlay.Create();
        }

        /// <inheritdoc />
        protected override async Task OnCleanup() {
            // The run stopped before the scenario's preparation: nothing to put back.
            if (m_SavedParameters == null) {
                return;
            }

            // A tool saves its parameters when it stops: the user's go back afterwards.
            m_ConnectTool.RequestDisable();
            await WaitFrames(5);

            var settings = NetworkToolsMod.Instance.Settings;

            settings.SavedParameterValues = m_SavedParameters;
            settings.ApplyAndSave();

            // The overlay comes last in the preparation, which may have stopped before it.
            m_Overlay?.Stop();
        }

        /// <summary>
        ///     Starts each test with no check counted, on the proving ground.
        ///     Keeps the game's terrain cull within its list, which the proving ground overruns.
        ///     Runs before each test: the runner counts no error of the scenario's preparation.
        ///     Once the game changes, each test fails with <see cref="LaneCullRoom" />'s error.
        /// </summary>
        [TestPrepare]
        private async Task PrepareTest() {
            m_Passed = 0;
            m_Failed = 0;
            m_Known  = false;

            m_Test++;
            m_Overlay.StartTest(m_Test, m_Tests.Length, m_Tests[m_Test - 1]);

            // Async, so that the runner awaits the error itself and not a reflection wrapper of it.
            LaneCullRoom.Arm();
            CheckTerrain();

            await Task.CompletedTask;
        }

        [Test]
        private async Task ConnectToolAcrossEachLane() {
            await UseConnectTool(true);

            foreach (Lane lane in System.Enum.GetValues(typeof(Lane))) {
                var expected = ProvingGround.Mouths(lane);

                if (expected != null) {
                    await Connect(string.Empty, lane, 0f, nameof(RoadPrefab), SmallRoad, expected);
                }
            }

            Finish("Connect");
        }

        [Test]
        private async Task ConnectToolWithOtherNetworksAndOnABend() {
            await UseConnectTool(true);

            // A track has no zoning grid: nothing of it is snapped to 4 m.
            await Connect(
                ", track",
                Lane.Sawtooth,
                -40f,
                nameof(TrackPrefab),
                TrainTrack,
                ProvingGround.Mouths(Lane.Sawtooth, TrackHalfWidth));

            // A path's limit is 2 m: its mouths are where the flanks have 6 m.
            // It is 3 m wide, too narrow for a mouth to move out.
            await Connect(
                ", path",
                Lane.Ridge,
                -20f,
                nameof(PathwayPrefab),
                PavementPath,
                ProvingGround.RidgeMouths(0f, 6f));

            // A bend through the dome: the mouths are read on the curve.
            var bend = Bend(
                new float2(0f, -400f),
                new float2(0f, -100f),
                new float2(100f, 0f),
                new float2(400f, 0f));

            await LayBend(bend, "Connect, bend");

            Finish("Connect, more");
        }

        [Test]
        private async Task ConnectToolAcrossRoads() {
            // Two roads across the lane: one on the ground between two humps, one on the next hump.
            var ground = NetUtils.StraightCurve(
                ProvingGround.Point(Lane.Humps, 712f, -20f),
                ProvingGround.Point(Lane.Humps, 712f, 20f));

            var top = NetUtils.StraightCurve(
                ProvingGround.Ground(Lane.Humps, 770f, -20f),
                ProvingGround.Ground(Lane.Humps, 770f, 20f));

            await UseConnectTool(false);
            await Lay(nameof(RoadPrefab), SmallRoad, ground);
            await Lay(nameof(RoadPrefab), SmallRoad, top);

            // The road starts past the hump that may go either way, on a sample of a whole lane's.
            // Each mouth goes out 7 m from 12 m of cover.
            var expected = new[] { 643f, 673f, 751f, 789f, 874.5f, 945.5f };

            await UseConnectTool(true);
            await Connect(
                ", across roads",
                Lane.Humps,
                0f,
                nameof(RoadPrefab),
                SmallRoad,
                expected,
                601f);

            // The road on the ground gets a junction, the one over the tunnel none.
            var joined = NetworkReader.NodesAlong(World.EntityManager, ground.a, ground.d);
            var over   = NetworkReader.NodesAlong(World.EntityManager, top.a, top.d);

            CheckNode("Connect, across a road on the ground", joined, 20f, Tolerance);
            Report(
                over.Count == 2,
                $"Connect, under a road on a hump, no junction: {over.Count} nodes");

            // A road pinned under the ridge at its middle: the game tunnels it by its own rule.
            // Its tunnel climbs from the middle to a mouth on the tilt, whose head wall slants.
            // A level road crosses it 16 m inside that mouth: the game's tunnel stays as it was.
            var start  = ProvingGround.Point(Lane.RidgeTiltRight, Start, 40f);
            var middle = ProvingGround.Point(Lane.RidgeTiltRight, 490f, 40f);
            var end    = ProvingGround.Point(Lane.RidgeTiltRight, End, 40f);

            await UseConnectTool(false);
            await Lay(nameof(RoadPrefab), SmallRoad, NetUtils.StraightCurve(start, middle));
            await Lay(nameof(RoadPrefab), SmallRoad, NetUtils.StraightCurve(middle, end));

            var before = State(Lane.RidgeTiltRight, 40f, false, false);
            var joint  = ProvingGround.Point(Lane.RidgeTiltRight, before[1] - 16f, 40f);

            joint.y = NetworkReader.EdgeHeight(World.EntityManager, joint.xz);

            // Both of its ends are under the ridge.
            var branch = NetUtils.StraightCurve(
                joint + new float3(0f, 0f, 20f),
                joint - new float3(0f, 0f, 20f));

            await UseConnectTool(true);
            await Lay(nameof(RoadPrefab), SmallRoad, branch);

            var after = State(Lane.RidgeTiltRight, 40f, false, false);
            var name  = "Connect, across a tunnel of the game's";

            CheckNode(name, Nodes(Lane.RidgeTiltRight, 40f), before[1] - 16f, Tolerance);
            CheckSame(name, before, after, Tolerance);

            Finish("Connect, across roads");
        }

        [Test]
        private async Task SlopeToolOnAGroundRoad() {
            var deep   = ProvingGround.Mouths(Lane.Deep);
            var gentle = ProvingGround.Mouths(Lane.Gentle);

            // A plain node in the way of a mouth is moved to it.
            await Slope(new SlopeCase {
                Name     = "plain node",
                Lane     = Lane.Deep,
                Across   = 40f,
                LaidNode = OwnNodeAlong,
                Expected = deep,
                Node     = deep[0],
            });

            // The same once the road is level and in a cut all along.
            // The game then merges that node away as it commits the cuts, and the mouth is cut.
            await Slope(new SlopeCase {
                Name       = "merged node",
                Lane       = Lane.Deep,
                Across     = -40f,
                LaidNode   = OwnNodeAlong,
                LevelFirst = true,
                Expected   = deep,
            });

            // A node with a side road stays, and with 10.5 m of ground it is the mouth.
            await Slope(new SlopeCase {
                Name       = "junction",
                Lane       = Lane.Gentle,
                Across     = -40f,
                SideRoadTo = -110f,
                Expected   = new[] { JointAlong, gentle[1] },
                Node       = JointAlong,
            });

            // A junction under 25 m of ground stays in the tunnel, its side road cut at a mouth.
            // The side road ends at the foot of the flank, so that it lies on the ground all along.
            // Level once sloped, its mouth is 26 m out, well clear of the least split length.
            await Slope(new SlopeCase {
                Name       = "junction in tunnel",
                Lane       = Lane.Hipped,
                Across     = 20f,
                Joint      = 490f,
                SideRoadTo = ProvingGround.LaneHalfWidth,
                SideCut    = true,
                LaidNode   = 490f,
                Expected   = ProvingGround.Mouths(Lane.Hipped),
                Node       = 490f,
            });

            // A plain node under 11 m of ground, 10 m before the point with 12 m, is the mouth.
            // The path is selected from its end, so that the tool runs against it.
            await Slope(new SlopeCase {
                Name     = "node within reach",
                Lane     = Lane.Staircase,
                Across   = 40f,
                Joint    = 690f,
                LaidNode = 690f,
                Reversed = true,
                Expected = new[] { 690f, float.NaN },
                Node     = 690f,
            });

            // An arch: the road sags at its middle, and the mouths go out by twice the sag there.
            // On the Ridge's flanks the mouths move out to 8.5 m, well within the reach.
            // The west one falls a metre from the game's own node, with 9 m: that node is one.
            await Slope(new SlopeCase {
                Name       = "arch",
                Lane       = Lane.Ridge,
                Across     = -40f,
                LevelFirst = true,
                Template   = ShapeTransformTemplate.SlopeArch,
                Expected   = new[] { OwnNodeAlong, ArchMouth(Lane.Ridge, 823f, ArchSag) },
            });

            // A track the game tunnelled itself, from two limits of cover, is cut again.
            // Its cuts are not snapped, and land where the formulas put them.
            // The two pieces meet on the flat: a joint inside a ridge leaves no room to cut there.
            // The game's own node on the first flank has 8 m: too near the mouth to cut, it is one.
            var track = ProvingGround.Mouths(Lane.Twins, TrackHalfWidth);

            track[0] = 156f;

            await Slope(new SlopeCase {
                Name            = "track",
                Lane            = Lane.Twins,
                Across          = 40f,
                Type            = nameof(TrackPrefab),
                Network         = TrainTrack,
                Joint           = 100f,
                Level           = true,
                KnownFirstApply = true,
                Expected        = track,
                CutWithin       = Tolerance,
            });

            // A track the game laid on viaducts over the ridges, too steep for it.
            await Slope(new SlopeCase {
                Name            = "viaduct track",
                Lane            = Lane.Twins,
                Across          = -40f,
                Type            = nameof(TrackPrefab),
                Network         = TrainTrack,
                KnownFirstApply = true,
                Expected        = ProvingGround.Mouths(Lane.Twins, TrackHalfWidth),
                CutWithin       = Tolerance,
            });

            // A plain node on the tunnel's side of a mouth moves out to it too.
            // Sagged 16 m at its middle, the road has 13 m of cover at the game's own node.
            var sagged = new[] { ArchMouth(Lane.Deep, 145f, 16f), ArchMouth(Lane.Deep, 905f, 16f) };

            await Slope(new SlopeCase {
                Name     = "node in the tunnel",
                Lane     = Lane.Deep,
                Across   = 20f,
                LaidNode = OwnNodeAlong,
                Template = ShapeTransformTemplate.SlopeArch,
                Sag      = 16f,
                Expected = sagged,
                Node     = sagged[0],
            });

            // Over the ravine down to the flat ground, the formulas' two mouths are 15.5 m apart.
            // The game's node 15 m before the first is slid to it.
            // The second cut stays 16.32 m from that node, as near as the game lets it be.
            var ravines = ProvingGround.Mouths(Lane.Ravines);

            ravines[6] = ravines[5] + 16.32f;

            await Slope(new SlopeCase {
                Name     = "ravines",
                Lane     = Lane.Ravines,
                Across   = 40f,
                Expected = ravines,
            });

            // A junction in the notch across the plateau, on a road made level with Tunnel off.
            // Its edges are laid again in place, open cuts now, tunnels once applied.
            // The notch's floor slants: a head wall there would not be level, but none is needed.
            await Slope(new SlopeCase {
                Name        = "junction in a notch",
                Lane        = Lane.Plateau,
                Across      = 60f,
                Joint       = 650f,
                SecondJoint = 675f,
                SideRoadTo  = 30f,
                LaidNode    = 650f,
                LevelFirst  = true,
                Expected    = ProvingGround.Mouths(Lane.Plateau),
            });

            await SlopeBend();
            await SlopeBuiltTunnel();
            await SlopeSwitchedAway();
            await SlopeLongSideRoad();

            Finish("Slope");
        }

        [Test]
        private async Task ParallelToolAlongTheRidge() {
            const float lift = 6f;
            const float drop = 4f;

            var a = ProvingGround.Point(Lane.Ridge, Start, 40f);
            var d = ProvingGround.Point(Lane.Ridge, End, 40f);

            await UseConnectTool(true);
            await Lay(nameof(RoadPrefab), SmallRoad, NetUtils.StraightCurve(a, d));
            await UseParallelTool();

            // A positive offset puts the copy to the right of the source.
            await Parallel(
                "higher copy",
                a,
                d,
                20f,
                lift,
                ParallelDirection.Same,
                20f,
                ProvingGround.RidgeMouths(lift));

            // A lower copy on the other side, running the other way, finds its cover further out.
            // The copy is cut edge by edge: its mouths fall in the source's 16 m portal edges.
            // A copy lower by more than 4 m keeps a portal edge from their far end instead.
            await Parallel(
                "lower copy, reverse",
                a,
                d,
                -20f,
                -drop,
                ParallelDirection.Reverse,
                60f,
                ProvingGround.RidgeMouths(-drop));

            Finish("Parallel");
        }

        [Test]
        private async Task ParallelToolOnABend() {
            var bend = Bend(
                new float2(-400f, -100f),
                new float2(-150f, -100f),
                new float2(-100f, -150f),
                new float2(-100f, -400f));

            await UseConnectTool(true);
            await LayBend(bend, "Parallel, bend, source");
            await UseParallelTool();

            // A copy on either side is cut on its own ground.
            // Built, the source and the copies before are read too.
            var built = 2;

            foreach (var offset in new[] { 20f, -20f }) {
                var name = $"Parallel, bend, copy at {offset}";

                m_ParallelTool.DebugSelectNear(bend.a, bend.d);
                m_ParallelTool.HorizontalOffset.Value = offset;
                m_ParallelTool.VerticalOffset.Value   = 0f;
                m_ParallelTool.ReverseDirection.Value = ParallelDirection.Same;
                m_ParallelTool.Tunnel.Value           = true;
                await Settle(10);
                CheckMouths(
                    $"{name}, preview",
                    BendMouths(bend, true),
                    2,
                    Deep,
                    CoverTolerance);

                m_ParallelTool.RequestApply();
                await Settle(30);
                built += 2;
                CheckMouths(
                    $"{name}, built",
                    BendMouths(bend, false),
                    built,
                    Deep,
                    CoverTolerance);
            }

            Finish("Parallel, bend");
        }

        /// <summary>
        ///     Lays a network along a lane with the Connect tool, and checks its tunnels.
        /// </summary>
        /// <param name="name">Name of the case, for the log, after "Connect".</param>
        /// <param name="lane">The lane.</param>
        /// <param name="across">Distance of the network to the left of the lane's middle.</param>
        /// <param name="type">Type of the network prefab.</param>
        /// <param name="network">Name of the network prefab.</param>
        /// <param name="expected">Distances along the lane, two for each tunnel.</param>
        /// <param name="start">Distance along the lane where the network starts.</param>
        private async Task Connect(
            string  name,
            Lane    lane,
            float   across,
            string  type,
            string  network,
            float[] expected,
            float   start = Start) {
            var from = ProvingGround.Point(lane, start, across);
            var to   = ProvingGround.Point(lane, End, across);

            LayPreview(type, network, NetUtils.StraightCurve(from, to));
            await Settle(10);
            Check(lane, $"Connect{name}, preview", expected, Tunnels(lane, across, true));

            m_ConnectTool.RequestApply();
            await Settle(30);
            Check(lane, $"Connect{name}, built", expected, Tunnels(lane, across, false));
        }

        /// <summary>
        ///     Lays a ground road along a lane, then makes it level with the Slope tool, twice.
        /// </summary>
        /// <param name="c">The case.</param>
        private async Task Slope(SlopeCase c) {
            var start = ProvingGround.Ground(c.Lane, Start, c.Across);
            var joint = JointPoint(c, c.Joint);
            var end   = ProvingGround.Ground(c.Lane, End, c.Across);
            var name  = $"Slope, {c.Name}";
            var three = !float.IsNaN(c.SecondJoint);
            var next  = three ? JointPoint(c, c.SecondJoint) : end;

            // The game merges the joint of two pieces in line up a slope, and keeps its own nodes.
            await UseConnectTool(false);
            await Lay(c.Type, c.Network, NetUtils.StraightCurve(start, joint));
            await Lay(c.Type, c.Network, NetUtils.StraightCurve(joint, next));

            if (three) {
                await Lay(c.Type, c.Network, NetUtils.StraightCurve(next, end));
            }

            if (!float.IsNaN(c.SideRoadTo)) {
                var side = ProvingGround.Ground(c.Lane, c.Joint, c.SideRoadTo);

                await Lay(c.Type, c.Network, NetUtils.StraightCurve(joint, side));

                if (three) {
                    side = ProvingGround.Ground(c.Lane, c.SecondJoint, c.SideRoadTo);

                    await Lay(c.Type, c.Network, NetUtils.StraightCurve(next, side));
                }
            }

            if (!float.IsNaN(c.LaidNode)) {
                CheckNode($"{name}, as laid", Nodes(c.Lane, c.Across), c.LaidNode, Still);
            }

            if (three) {
                CheckNode($"{name}, as laid", Nodes(c.Lane, c.Across), c.SecondJoint, Still);
            }

            await UseSlopeTool(ShapeTransformTemplate.SlopeLinear);

            if (c.LevelFirst) {
                m_SlopeTool.DebugSelectNear(start, end);
                m_SlopeTool.Tunnel.Value = false;
                await Settle(10);

                m_SlopeTool.RequestApply();
                await Settle(30);
            }

            if (c.Template != ShapeTransformTemplate.SlopeLinear) {
                await UseSlopeTool(c.Template);
            }

            if (c.Template == ShapeTransformTemplate.SlopeArch) {
                m_SlopeTool.ArchHeight.Value   = -c.Sag;
                m_SlopeTool.ArchPosition.Value = 0.5f;
            }

            var sideArea = c.SideCut ? SideArea(c) : default;

            await SlopeTwice(
                name,
                c.Reversed ? end : start,
                c.Reversed ? start : end,
                (what, preview, exact) => {
                    var within = exact ? Tolerance : c.CutWithin;

                    Check(c.Lane, what, c.Expected, Tunnels(c.Lane, c.Across, preview), within);

                    // The preview holds what the tool would change, and the second pass
                    // changes nothing of a side road the first one cut: it is not in there.
                    // The side road's mouth is built where it was previewed, snapped or not.
                    if (c.SideCut && (!preview || exact)) {
                        var mouths = NetworkReader.Mouths(World.EntityManager, sideArea, preview);

                        CheckMouths($"{what}, side road", mouths, 1, Shallow, CoverTolerance);
                    }
                },
                preview => State(c.Lane, c.Across, preview, !c.LevelFirst),
                c.KnownFirstApply,
                c.CutWithin);

            if (!float.IsNaN(c.Node)) {
                CheckNode(name, Nodes(c.Lane, c.Across), c.Node, Tolerance);
            }
        }

        /// <summary>
        ///     Lays a ground road on a bend through the dome and makes it level: a curved tunnel.
        ///     The ground road flattens the flank across its width where it crosses it obliquely.
        ///     The tool measures on that ground, so a mouth is up to a metre inside the formulas'.
        ///     The ground slants across the road under 12 m: the mouths stay at three limits.
        /// </summary>
        private async Task SlopeBend() {
            var bend = Bend(
                new float2(0f, 400f),
                new float2(0f, 100f),
                new float2(-100f, 100f),
                new float2(-400f, 100f));

            await UseConnectTool(false);
            await Lay(nameof(RoadPrefab), SmallRoad, bend);
            await UseSlopeTool(ShapeTransformTemplate.SlopeLinear);

            await SlopeTwice(
                "Slope, bend",
                bend.a,
                bend.d,
                (what, preview, exact) => CheckMouths(
                    what,
                    BendMouths(bend, preview),
                    2,
                    Deep,
                    CutCoverTolerance),
                preview => Flat(BendMouths(bend, preview)),
                false,
                Still);
        }

        /// <summary>
        ///     Makes the tunnel road the Connect tool built along the Ridge level: nothing moves.
        /// </summary>
        private async Task SlopeBuiltTunnel() {
            const string name = "Slope, built tunnel";

            var before = State(Lane.Ridge, 0f, false, true);

            await UseSlopeTool(ShapeTransformTemplate.SlopeLinear);
            await SlopeTwice(
                name,
                ProvingGround.Point(Lane.Ridge, Start, 0f),
                ProvingGround.Point(Lane.Ridge, End, 0f),
                (what, preview, exact) => Check(
                    Lane.Ridge,
                    what,
                    ProvingGround.Mouths(Lane.Ridge),
                    Tunnels(Lane.Ridge, 0f, preview)),
                preview => State(Lane.Ridge, 0f, preview, true),
                false,
                Still);

            CheckSame($"{name}, as built", before, State(Lane.Ridge, 0f, false, true), Still);
        }

        /// <summary>
        ///     Starts an apply on a road with a side road, and switches tool before it ends.
        ///     Opening the Slope tool again changes nothing: the apply cut short is not taken up.
        /// </summary>
        private async Task SlopeSwitchedAway() {
            const string name = "Slope, switched away";

            var start = ProvingGround.Ground(Lane.Gentle, Start, 40f);
            var joint = ProvingGround.Ground(Lane.Gentle, JointAlong, 40f);
            var end   = ProvingGround.Ground(Lane.Gentle, End, 40f);
            var side  = ProvingGround.Ground(Lane.Gentle, JointAlong, 110f);
            var area  = new Bounds2(
                ProvingGround.Point(Lane.Gentle, JointAlong - 30f, 40f).xz,
                ProvingGround.Point(Lane.Gentle, JointAlong + 30f, 110f).xz);

            await UseConnectTool(false);
            await Lay(nameof(RoadPrefab), SmallRoad, NetUtils.StraightCurve(start, joint));
            await Lay(nameof(RoadPrefab), SmallRoad, NetUtils.StraightCurve(joint, end));
            await Lay(nameof(RoadPrefab), SmallRoad, NetUtils.StraightCurve(joint, side));
            await UseSlopeTool(ShapeTransformTemplate.SlopeLinear);

            m_SlopeTool.DebugSelectNear(start, end);
            m_SlopeTool.Tunnel.Value = true;
            await Settle(Previewed);

            // The first frame of the apply cuts the road, the next ones would slope it.
            m_SlopeTool.RequestApply();
            await WaitFrames(1);
            await UseConnectTool(false);

            var before = NetworkReader.Ends(World.EntityManager, area);

            await UseSlopeTool(ShapeTransformTemplate.SlopeLinear);
            await WaitFrames(Applied);

            var after = NetworkReader.Ends(World.EntityManager, area);

            CheckSame($"{name}, opened again", before, after, Still);
        }

        /// <summary>
        ///     Makes a road level, its long side road across the plateau from a junction 30 m down.
        ///     The side road's first edge is cut at its mouth, 73 m out, and nowhere before it.
        /// </summary>
        private async Task SlopeLongSideRoad() {
            const string name = "Slope, long side road";

            var start    = ProvingGround.Ground(Lane.Plateau, Start, -40f);
            var joint    = ProvingGround.Ground(Lane.Plateau, 500f, -40f);
            var end      = ProvingGround.Ground(Lane.Plateau, End, -40f);
            var side     = ProvingGround.Ground(Lane.Plateau, 580f, 40f);
            var expected = ProvingGround.Mouths(Lane.Plateau);

            await UseConnectTool(false);
            await Lay(nameof(RoadPrefab), SmallRoad, NetUtils.StraightCurve(start, joint));
            await Lay(nameof(RoadPrefab), SmallRoad, NetUtils.StraightCurve(joint, end));
            await Lay(nameof(RoadPrefab), SmallRoad, NetUtils.StraightCurve(joint, side));
            await UseSlopeTool(ShapeTransformTemplate.SlopeLinear);

            await SlopeTwice(
                name,
                start,
                end,
                (what, preview, exact) => {
                    var found = Tunnels(Lane.Plateau, -40f, preview);

                    Check(Lane.Plateau, what, expected, found, exact ? Tolerance : CutTolerance);

                    if (preview) {
                        return;
                    }

                    // A node between the junction and the mouth is one the preview never showed.
                    var nodes = NetworkReader.NodesAlong(World.EntityManager, joint, side)
                                             .FindAll(along => along > 5f && along < 60f);

                    CheckSame($"{what}, side road before its mouth", new List<float>(), nodes, 0f);
                },
                preview => State(Lane.Plateau, -40f, preview, true),
                false,
                CutTolerance);
        }

        /// <summary>
        ///     Makes a path level with the Slope tool, Tunnel on, twice.
        ///     The preview has its mouths where the apply builds them.
        ///     The second pass must change nothing.
        /// </summary>
        /// <param name="name">Name of the case, for the log.</param>
        /// <param name="start">A point near the first node of the path.</param>
        /// <param name="end">A point near the last node of the path.</param>
        /// <param name="check">
        ///     Checks the preview or the built road.
        ///     It takes what was read, true for the preview, and true for the first preview.
        /// </param>
        /// <param name="state">
        ///     Reads the preview or the built road, as numbers that must not change.
        ///     Those of the preview come first in the built road's.
        /// </param>
        /// <param name="knownFirstApply">True when the two applies build different nodes.</param>
        /// <param name="within">Distance a mouth may move from the preview to the apply.</param>
        private async Task SlopeTwice(
            string                            name,
            float3                            start,
            float3                            end,
            System.Action<string, bool, bool> check,
            System.Func<bool, List<float>>    state,
            bool                              knownFirstApply,
            float                             within) {
            var before = new List<float>();

            for (var pass = 1; pass <= 2; pass++) {
                m_SlopeTool.DebugSelectNear(start, end);

                m_SlopeTool.Tunnel.Value = true;
                await Settle(Previewed);

                check($"{name}, preview {pass}", true, pass == 1);

                var previewed = state(true);

                m_SlopeTool.RequestApply();
                await Settle(Applied);

                check($"{name}, built {pass}", false, false);

                var after = state(false);
                var built = after.GetRange(0, math.min(previewed.Count, after.Count));

                CheckSame($"{name}, built {pass} as previewed", previewed, built, within);

                if (pass == 2) {
                    m_Known = knownFirstApply;

                    CheckSame(name, before, after, Still);
                }

                m_Known = false;
                before  = after;
            }
        }

        /// <summary>
        ///     Copies the road between two points with the Parallel tool, and checks the copy.
        /// </summary>
        /// <param name="name">Name of the case, for the log.</param>
        /// <param name="a">A point near the first node of the source.</param>
        /// <param name="d">A point near the last node of the source.</param>
        /// <param name="offset">Horizontal offset of the copy, positive to the right.</param>
        /// <param name="lift">Vertical offset of the copy.</param>
        /// <param name="direction">Direction of the copy.</param>
        /// <param name="across">Distance of the copy to the left of the lane's middle.</param>
        /// <param name="expected">Distances along the lane, two for each tunnel.</param>
        private async Task Parallel(
            string            name,
            float3            a,
            float3            d,
            float             offset,
            float             lift,
            ParallelDirection direction,
            float             across,
            float[]           expected) {
            m_ParallelTool.DebugSelectNear(a, d);
            m_ParallelTool.HorizontalOffset.Value = offset;
            m_ParallelTool.VerticalOffset.Value   = lift;
            m_ParallelTool.ReverseDirection.Value = direction;
            m_ParallelTool.Tunnel.Value           = true;
            await Settle(10);
            Check(
                Lane.Ridge,
                $"Parallel, {name}, preview",
                expected,
                Tunnels(Lane.Ridge, across, true));

            m_ParallelTool.RequestApply();
            await Settle(30);
            Check(
                Lane.Ridge,
                $"Parallel, {name}, built",
                expected,
                Tunnels(Lane.Ridge, across, false));
        }

        /// <summary>
        ///     Waits for the edges to change, the preview's included, then to hold still.
        ///     Waits no more than a number of frames, which a step that changes nothing takes.
        /// </summary>
        /// <param name="most">Most frames to wait.</param>
        private async Task Settle(int most) {
            var start = NetworkReader.Fingerprint(World.EntityManager);
            var last  = start;
            var moved = false;
            var still = 0;

            for (var frame = 0; frame < most; frame++) {
                await WaitFrames(1);

                var now = NetworkReader.Fingerprint(World.EntityManager);

                moved |= now != start;
                still =  now == last ? still + 1 : 0;
                last  =  now;

                if (moved && still >= Quiet) {
                    return;
                }
            }
        }

        /// <summary>
        ///     Puts the camera straight above the proving ground, all of it in view.
        ///     The lanes stand up the screen, side by side, the first one on the left.
        /// </summary>
        private void LookDown() {
            var cameras = World.GetOrCreateSystemManaged<CameraUpdateSystem>();
            var lens    = cameras.activeCamera;
            var area    = ProvingGround.Area();
            var centre  = MathUtils.Center(area);
            var half    = MathUtils.Size(area) * (0.5f * ViewMargin);
            var across  = Camera.VerticalToHorizontalFieldOfView(lens.fieldOfView, lens.aspect);
            var high    = math.tan(math.radians(lens.fieldOfView * 0.5f));
            var wide    = math.tan(math.radians(across * 0.5f));
            var camera  = cameras.gamePlayController;

            // With the camera turned to -x, x runs down the screen and z to the right.
            camera.pivot = new Vector3(centre.x, ProvingGround.Base, centre.y);
            camera.angle = new float2(-90f, 90f);
            camera.zoom  = math.max(half.x / high, half.y / wide);
        }

        /// <summary>
        ///     Clears the editor's start tiles, and with them the city boundary drawn around them.
        ///     The boundary is drawn again once the tiles are marked updated.
        /// </summary>
        private void ClearStartTiles() {
            var tiles  = World.GetOrCreateSystemManaged<MapTileSystem>().GetStartTiles();
            var former = tiles.ToArray(Allocator.Temp);

            tiles.Clear();
            World.EntityManager.AddComponent<Updated>(former);
            former.Dispose();
        }

        /// <summary>
        ///     Makes the Connect tool the active one.
        /// </summary>
        /// <param name="tunnel">True to turn Tunnel mode on.</param>
        private async Task UseConnectTool(bool tunnel) {
            m_ConnectTool.RequestEnable();
            await WaitFrames(5);

            // The tool restores its saved parameters when it starts: set this one afterwards.
            m_ConnectTool.Tunnel.Value = tunnel;
        }

        /// <summary>
        ///     Makes the Slope tool the active one, with a template.
        /// </summary>
        /// <param name="template">The template.</param>
        private async Task UseSlopeTool(ShapeTransformTemplate template) {
            m_SlopeTool.RequestEnable();
            await WaitFrames(5);

            // A change of template resets the tool a frame later: select afterwards.
            m_SlopeTool.Template.Value = template;
            await WaitFrames(5);
        }

        /// <summary>
        ///     Makes the Parallel tool the active one.
        /// </summary>
        private async Task UseParallelTool() {
            m_ParallelTool.RequestEnable();
            await WaitFrames(5);
        }

        /// <summary>
        ///     Previews a network on a curve with the Connect tool, no node selected.
        /// </summary>
        /// <param name="type">Type of the network prefab.</param>
        /// <param name="network">Name of the network prefab.</param>
        /// <param name="curve">The curve.</param>
        private void LayPreview(string type, string network, Bezier4x3 curve) {
            m_ConnectTool.DebugFree(type, network, curve.a, curve.b, curve.c, curve.d);
        }

        /// <summary>
        ///     Builds a network on a curve with the Connect tool, no node selected.
        /// </summary>
        /// <param name="type">Type of the network prefab.</param>
        /// <param name="network">Name of the network prefab.</param>
        /// <param name="curve">The curve.</param>
        private async Task Lay(string type, string network, Bezier4x3 curve) {
            LayPreview(type, network, curve);
            await Settle(10);

            m_ConnectTool.RequestApply();
            await Settle(30);
        }

        /// <summary>
        ///     Lays a small road on a bend with the Connect tool.
        ///     Checks its two mouths in the preview and as built.
        ///     A bend through the dome crosses its flank obliquely: its mouths stay at 12 m.
        /// </summary>
        /// <param name="bend">The curve.</param>
        /// <param name="name">Name of the case, for the log.</param>
        private async Task LayBend(Bezier4x3 bend, string name) {
            LayPreview(nameof(RoadPrefab), SmallRoad, bend);
            await Settle(10);
            CheckMouths($"{name}, preview", BendMouths(bend, true), 2, Deep, CoverTolerance);

            m_ConnectTool.RequestApply();
            await Settle(30);
            CheckMouths($"{name}, built", BendMouths(bend, false), 2, Deep, CoverTolerance);
        }

        /// <summary>
        ///     Makes a bend through the dome, from four points given as offsets from its centre.
        ///     A quarter turn with its ends 400 m out and its handles 100 m out is under the top.
        /// </summary>
        /// <param name="a">Start of the curve, in x and z.</param>
        /// <param name="b">Handle of the start.</param>
        /// <param name="c">Handle of the end.</param>
        /// <param name="d">End of the curve.</param>
        /// <returns>The curve, at the height of the flat ground.</returns>
        private static Bezier4x3 Bend(float2 a, float2 b, float2 c, float2 d) {
            var centre = ProvingGround.DomeCentre();

            return new Bezier4x3(
                centre + new float3(a.x, 0f, a.y),
                centre + new float3(b.x, 0f, b.y),
                centre + new float3(c.x, 0f, c.y),
                centre + new float3(d.x, 0f, d.y));
        }

        /// <summary>
        ///     Gets the area around the side road of a case, its junction included.
        ///     The junction is no mouth: its tunnel edges are the side road's and the path's.
        /// </summary>
        /// <param name="c">The case.</param>
        /// <returns>The area, in x and z.</returns>
        private static Bounds2 SideArea(SlopeCase c) {
            var near = math.min(c.Across, c.SideRoadTo);
            var far  = math.max(c.Across, c.SideRoadTo);

            return new Bounds2(
                ProvingGround.Point(c.Lane, c.Joint - 30f, near).xz,
                ProvingGround.Point(c.Lane, c.Joint + 30f, far).xz);
        }

        /// <summary>
        ///     Finds where an arch case's road has the cover of a mouth, near one of a level road.
        ///     The road sags on two parabolas, as the arch template shapes it.
        ///     Their shared peak is at the middle of the path.
        /// </summary>
        /// <param name="lane">The lane.</param>
        /// <param name="near">A mouth of the level road along the lane.</param>
        /// <param name="sag">Depth of the sag at the middle of the road.</param>
        /// <returns>The distance along the lane.</returns>
        private static float ArchMouth(Lane lane, float near, float sag) {
            var low    = near - 20f;
            var high   = near + 20f;
            var rising = ArchCover(lane, low, sag) < ArchCover(lane, high, sag);

            for (var i = 0; i < 24; i++) {
                var middle = (low + high) * 0.5f;

                if ((ArchCover(lane, middle, sag) < Shallow) == rising) {
                    low = middle;
                } else {
                    high = middle;
                }
            }

            return (low + high) * 0.5f;
        }

        /// <summary>
        ///     Gets the ground cover of an arch case's road along its lane.
        /// </summary>
        /// <param name="lane">The lane.</param>
        /// <param name="along">Distance along the lane.</param>
        /// <param name="sag">Depth of the sag at the middle of the road.</param>
        /// <returns>The cover.</returns>
        private static float ArchCover(Lane lane, float along, float sag) {
            var ratio = (along - Start) / (End - Start);
            var depth = sag * (1f - 4f * (ratio - 0.5f) * (ratio - 0.5f));

            return ProvingGround.Relief(lane, along, 0f) + depth;
        }

        /// <summary>
        ///     Gets a joint of a case's ground road.
        /// </summary>
        /// <param name="c">The case.</param>
        /// <param name="along">Distance of the joint along the lane.</param>
        /// <returns>The joint, on the ground or at the height of the flat ground.</returns>
        private static float3 JointPoint(SlopeCase c, float along) {
            return c.Level
                ? ProvingGround.Point(c.Lane, along, c.Across)
                : ProvingGround.Ground(c.Lane, along, c.Across);
        }

        /// <summary>
        ///     Checks that the game holds the heights of the formulas, on top of the first ridge.
        /// </summary>
        private void CheckTerrain() {
            var terrain = World.GetOrCreateSystemManaged<TerrainSystem>().GetHeightData(true);
            var top     = ProvingGround.Point(Lane.Ridge, 490f, 0f);
            var height  = TerrainUtils.SampleHeight(ref terrain, top);

            if (math.abs(height - ProvingGround.Height(top.xz)) > 0.01f) {
                throw new TestScenarioException($"The ridge is {height} m high: no ground.");
            }
        }

        /// <summary>
        ///     Gets the tunnels of a road along a lane (<see cref="NetworkReader.Tunnels" />).
        /// </summary>
        /// <param name="lane">The lane.</param>
        /// <param name="across">Distance of the road to the left of the lane's middle.</param>
        /// <param name="preview">True to read the preview, false to read what is built.</param>
        /// <returns>The tunnels, as distances along the lane, in order.</returns>
        private List<Bounds1> Tunnels(Lane lane, float across, bool preview) {
            return NetworkReader.Tunnels(World.EntityManager, lane, across, preview);
        }

        /// <summary>
        ///     Gets the built nodes of a road along a lane (<see cref="NetworkReader.Nodes" />).
        /// </summary>
        /// <param name="lane">The lane.</param>
        /// <param name="across">Distance of the road to the left of the lane's middle.</param>
        /// <returns>The nodes, as distances along the lane, in order.</returns>
        private List<float> Nodes(Lane lane, float across) {
            return NetworkReader.Nodes(World.EntityManager, lane, across);
        }

        /// <summary>
        ///     Gets the mouths around a bend, within <see cref="BendMargin" /> of its bounds.
        /// </summary>
        /// <param name="bend">The curve.</param>
        /// <param name="preview">True to read the preview, false to read what is built.</param>
        /// <returns>The mouths, in order of x then z.</returns>
        private List<Mouth> BendMouths(Bezier4x3 bend, bool preview) {
            var area = MathUtils.Expand(MathUtils.Bounds(bend.xz), BendMargin);

            return NetworkReader.Mouths(World.EntityManager, area, preview);
        }

        /// <summary>
        ///     Reads a road along a lane as numbers that must not change.
        ///     Those are the mouths, then the built nodes.
        ///     At each commit the game merges plain nodes between edges of one kind away.
        ///     On a road in a cut all along, the nodes outside the tunnels come and go.
        /// </summary>
        /// <param name="lane">The lane.</param>
        /// <param name="across">Distance of the road to the left of the lane's middle.</param>
        /// <param name="preview">True to read the preview, false to read what is built.</param>
        /// <param name="withNodes">True to read the built nodes too.</param>
        /// <returns>The numbers.</returns>
        private List<float> State(Lane lane, float across, bool preview, bool withNodes) {
            var tunnels = Tunnels(lane, across, preview);
            var state   = new List<float>();

            for (var i = 0; i < tunnels.Count; i++) {
                state.Add(tunnels[i].min);
                state.Add(tunnels[i].max);
            }

            if (withNodes && !preview) {
                state.AddRange(Nodes(lane, across));
            }

            return state;
        }

        /// <summary>
        ///     Reads mouths as numbers that must not change: the x and z of each.
        /// </summary>
        /// <param name="mouths">The mouths.</param>
        /// <returns>The numbers.</returns>
        private static List<float> Flat(List<Mouth> mouths) {
            var state = new List<float>();

            for (var i = 0; i < mouths.Count; i++) {
                state.Add(mouths[i].Position.x);
                state.Add(mouths[i].Position.z);
            }

            return state;
        }

        /// <summary>
        ///     Logs the totals of a test and fails it when a check failed.
        /// </summary>
        /// <param name="tool">The tool under test, for the log.</param>
        private void Finish(string tool) {
            log.Info($"NetworkTools: Tunnel mode, {tool}: {m_Passed} passed, {m_Failed} failed");

            if (m_Failed > 0) {
                throw new TestScenarioException($"{m_Failed} checks failed, see the lines above.");
            }
        }

        /// <summary>
        ///     Compares the tunnels found along a lane with the ones expected, and logs the result.
        ///     Skips a mouth expected as not a number.
        /// </summary>
        /// <param name="lane">The lane.</param>
        /// <param name="what">What was read, for the log.</param>
        /// <param name="expected">Distances along the lane, two for each tunnel.</param>
        /// <param name="found">The tunnels read from the game.</param>
        /// <param name="within">Distance a mouth may be from where it is expected.</param>
        private void Check(
            Lane          lane,
            string        what,
            float[]       expected,
            List<Bounds1> found,
            float         within = Tolerance) {
            var ok   = found.Count == expected.Length / 2;
            var text = string.Empty;

            for (var i = 0; i < found.Count; i++) {
                text += $"[{found[i].min:0.00} {found[i].max:0.00}] ";

                ok = ok
                     && Near(found[i].min, expected[i * 2], within)
                     && Near(found[i].max, expected[i * 2 + 1], within);
            }

            Report(
                ok,
                ok
                    ? $"{lane}, {what}: {text}"
                    : $"{lane}, {what}: found {text}expected {string.Join(" ", expected)}");
        }

        /// <summary>
        ///     Checks that the mouths found are as many as expected, each with the cover of one.
        /// </summary>
        /// <param name="what">What was read, for the log.</param>
        /// <param name="mouths">The mouths read from the game.</param>
        /// <param name="count">Number of mouths expected.</param>
        /// <param name="expected">Cover each mouth must have.</param>
        /// <param name="within">Cover a mouth may have over or under that.</param>
        private void CheckMouths(
            string      what,
            List<Mouth> mouths,
            int         count,
            float       expected,
            float       within) {
            var ok   = mouths.Count == count;
            var text = string.Empty;

            for (var i = 0; i < mouths.Count; i++) {
                var mouth = mouths[i];
                var cover = ProvingGround.Cover(mouth.Position, mouth.Tangent, HalfWidth);

                text += $"({mouth.Position.x:0.0}, {mouth.Position.z:0.0}) {cover:0.00} ";
                ok   =  ok && math.abs(cover - expected) <= within;
            }

            Report(ok, $"{what}, {count} mouths at {expected} m: {text}");
        }

        /// <summary>
        ///     Checks that a road has a node at a given place, and logs the result.
        /// </summary>
        /// <param name="what">What was read, for the log.</param>
        /// <param name="nodes">The nodes of a road, as distances along it.</param>
        /// <param name="wanted">Where a node must be.</param>
        /// <param name="within">Distance the node may be from there.</param>
        private void CheckNode(string what, List<float> nodes, float wanted, float within) {
            var ok   = nodes.Exists(node => math.abs(node - wanted) <= within);
            var text = string.Join(" ", nodes.ConvertAll(node => node.ToString("0.00")));

            Report(ok, $"{what}, a node at {wanted}: nodes {text}");
        }

        /// <summary>
        ///     Checks that two readings of a road are the same, and logs the result.
        /// </summary>
        /// <param name="what">What was read, for the log.</param>
        /// <param name="before">The mouths, then the nodes, as distances along the lane.</param>
        /// <param name="after">The same afterwards.</param>
        /// <param name="within">Distance a number may be from the one before it.</param>
        private void CheckSame(string what, List<float> before, List<float> after, float within) {
            var ok = before.Count == after.Count;

            for (var i = 0; ok && i < after.Count; i++) {
                ok = math.abs(after[i] - before[i]) <= within;
            }

            var was = string.Join(" ", before.ConvertAll(number => number.ToString("0.00")));
            var now = string.Join(" ", after.ConvertAll(number => number.ToString("0.00")));

            Report(ok, $"{what}, nothing moved: before {was}, after {now}");
        }

        /// <summary>
        ///     Counts a check and logs it: passed, known, or failed.
        /// </summary>
        /// <param name="ok">True when the check passed.</param>
        /// <param name="text">What was checked and read, for the log.</param>
        private void Report(bool ok, string text) {
            var result = ok ? "PASSED" : m_Known ? "KNOWN" : "FAILED";

            m_Passed += ok ? 1 : 0;
            m_Failed += ok || m_Known ? 0 : 1;

            log.Info($" [{result}] {text}");
            m_Overlay.Report(result, text);
        }

        /// <summary>
        ///     Checks that a mouth is near where it is expected.
        ///     Passes a mouth expected as not a number.
        /// </summary>
        /// <param name="found">Where the mouth is, along the lane.</param>
        /// <param name="expected">Where it is expected, or not a number.</param>
        /// <param name="within">Distance the mouth may be from there.</param>
        /// <returns>True if the mouth is near enough, or not compared.</returns>
        private static bool Near(float found, float expected, float within) {
            return float.IsNaN(expected) || math.abs(found - expected) <= within;
        }
    }
}
