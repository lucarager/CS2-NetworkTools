namespace NetworkTools.Tests {
    using Colossal.Mathematics;

    using NetworkTools.Systems.Tools.Utils;

    using Unity.Mathematics;

    /// <summary>
    ///     A terrain made for tests: flat ground with one lane of relief for each family of cases.
    ///     Every height comes from a formula, so a test works its numbers out instead of measuring.
    ///     A lane runs along x, its relief a function of the distance along and across it.
    ///     The breaks of slope a test measures at fall on posts of the heightmap.
    ///     A height is exact there, and between two posts the game interpolates as the formulas do.
    ///     Nothing here needs the game: the same heights feed the tests outside it.
    /// </summary>
    internal static class ProvingGround {
        /// <summary>
        ///     The lanes, in order of growing z.
        /// </summary>
        public enum Lane {
            /// <summary>
            ///     A ridge 42 m high across the lane, flanks rising one in two.
            /// </summary>
            Ridge,

            /// <summary>
            ///     The ridge of <see cref="Ridge" />, its top rising to the left of the lane.
            ///     The flat ground around it stays flat.
            /// </summary>
            RidgeTiltLeft,

            /// <summary>
            ///     The ridge of <see cref="Ridge" />, its top rising to the right of the lane.
            ///     The flat ground around it stays flat.
            /// </summary>
            RidgeTiltRight,

            /// <summary>
            ///     Plateaus 70 m long at 7 to 13 m, a step of one cell between two.
            ///     The plateau before the 12 m one has 11 m: a plain node there is the mouth.
            /// </summary>
            Staircase,

            /// <summary>
            ///     A ridge cut by six ravines, down to 2, 6, 0, 10, 3.5, and 4.5 m.
            ///     Three keep less than one limit of cover, three more: each is a piece of its own.
            /// </summary>
            Ravines,

            /// <summary>
            ///     A cliff of 28 m in one cell, square to the lane.
            /// </summary>
            Cliff,

            /// <summary>
            ///     The cliff turned 20 degrees from square.
            /// </summary>
            CliffSkew,

            /// <summary>
            ///     A rise of 15 in 100 to 30 m, and a fall of 8 in 100.
            /// </summary>
            Gentle,

            /// <summary>
            ///     Ground that rises across the lane and never covers the low side.
            /// </summary>
            Hillside,

            /// <summary>
            ///     Hills with 12 m of cover over 0, 4, 8, 16, 24, and 57 m.
            /// </summary>
            Humps,

            /// <summary>
            ///     Ridges 28 m high with 7, 14, and 28 m of flat ground between.
            /// </summary>
            Twins,

            /// <summary>
            ///     A ridge 70 m high, deeper than a road may go.
            /// </summary>
            Deep,

            /// <summary>
            ///     A pit 10 m deep, then a ridge.
            /// </summary>
            Pit,

            /// <summary>
            ///     A ridge with flanks on its four sides, for side roads.
            /// </summary>
            Hipped,

            /// <summary>
            ///     Eight ridges 21 m high and 84 m long, one after the other.
            /// </summary>
            Sawtooth,

            /// <summary>
            ///     A ridge 30 m high, its flat top as wide as the lane, for long side roads.
            ///     A notch 4 m long crosses its left side at 650 m, its floor slanting across.
            /// </summary>
            Plateau,
        }

        /// <summary>
        ///     Posts along each side of the heightmap.
        /// </summary>
        public const int Resolution = 4096;

        /// <summary>
        ///     Distance between two posts.
        /// </summary>
        public const float CellSize = 3.5f;

        /// <summary>
        ///     Height the heightmap's largest value stands for.
        ///     The game's default is 4096 m, which rounds a height to 6 cm.
        /// </summary>
        public const float HeightScale = 256f;

        /// <summary>
        ///     Height of the flat ground.
        /// </summary>
        public const float Base = 64f;

        /// <summary>
        ///     Length of a lane, its relief between 140 m and 910 m.
        /// </summary>
        public const float LaneLength = LanePosts * CellSize;

        /// <summary>
        ///     Distance from the middle of a lane to the edge of its relief.
        /// </summary>
        public const float LaneHalfWidth = LaneHalfPosts * CellSize;

        /// <summary>
        ///     Radius of the dome at its foot, its flank rising one in two to 42 m.
        /// </summary>
        public const float DomeRadius = 280f;

        /// <summary>
        ///     Length of a lane, in heightmap posts.
        /// </summary>
        private const int LanePosts = 300;

        /// <summary>
        ///     Half the width of a lane's relief, in posts.
        /// </summary>
        private const int LaneHalfPosts = 20;

        /// <summary>
        ///     Distance between the middles of two lanes, in posts.
        ///     The flat ground between two reliefs is as wide as a relief.
        /// </summary>
        private const int LanePitch = 60;

        /// <summary>
        ///     Number of lanes, one for each value of <see cref="Lane" />.
        /// </summary>
        private const int LaneCount = 16;

        /// <summary>
        ///     Post where every lane starts, near the middle of the map.
        /// </summary>
        private const int FirstPostX = 1848;

        /// <summary>
        ///     Post of the first lane's middle.
        /// </summary>
        private const int FirstPostZ = 1648;

        /// <summary>
        ///     Post of the dome's centre, past the end of the lanes.
        /// </summary>
        private const int DomePostX = FirstPostX + LanePosts + 120;

        /// <summary>
        ///     Post of the dome's centre, along the lanes' side.
        /// </summary>
        private const int DomePostZ = FirstPostZ + 120;

        /// <summary>
        ///     Heights of the plateaus of <see cref="Lane.Staircase" />, in order along the lane.
        /// </summary>
        private static readonly float[] s_Plateaus = {
            7f, 7.5f, 8f, 8.25f, 8.5f, 9f, 10f, 11f, 12f, 13f,
        };

        /// <summary>
        ///     Gets a point of a lane at the height of the flat ground.
        /// </summary>
        /// <param name="lane">The lane.</param>
        /// <param name="along">Distance from the start of the lane.</param>
        /// <param name="across">Distance to the left of the lane's middle.</param>
        /// <returns>The world position.</returns>
        public static float3 Point(Lane lane, float along, float across) {
            var middle = FirstPostZ + (int)lane * LanePitch;

            return new float3(World(FirstPostX) + along, Base, World(middle) + across);
        }

        /// <summary>
        ///     Gets a point of a lane on its ground.
        /// </summary>
        /// <param name="lane">The lane.</param>
        /// <param name="along">Distance from the start of the lane.</param>
        /// <param name="across">Distance to the left of the lane's middle.</param>
        /// <returns>The world position.</returns>
        public static float3 Ground(Lane lane, float along, float across) {
            var point = Point(lane, along, across);

            point.y += math.abs(across) > LaneHalfWidth ? 0f : Relief(lane, along, across);

            return point;
        }

        /// <summary>
        ///     Gets where the tunnels of a level road along the middle of a lane start and end.
        ///     The road lies at the height of the flat ground, and its limit is a road's, 4 m.
        ///     A mouth goes out from where the cover over the whole width reaches 12 m.
        ///     It stops where the cover falls to 8.5 m or the head wall slants.
        ///     It goes no further than the reach of the network's width.
        /// </summary>
        /// <param name="lane">The lane.</param>
        /// <param name="halfWidth">Half the network's width, a small road's by default.</param>
        /// <returns>
        ///     Distances along the lane, two for each tunnel, in order.
        ///     Not a number for a mouth that depends on where the road's samples fall.
        ///     Null for a lane its own test checks instead.
        /// </returns>
        public static float[] Mouths(Lane lane, float halfWidth = 8f) {
            var reach = TunnelRuns.Reach(halfWidth);

            switch (lane) {
                // The flanks rise one in two: 8.5 m is 7 m out.
                case Lane.Ridge:
                case Lane.Hipped:
                case Lane.Plateau:
                    return new[] { Out(164f, -7f, reach), Out(816f, 7f, reach) };

                // The ground slants across the road: only three limits keep the head wall level.
                // The low edge has them last, half the half width further in than the middle.
                case Lane.RidgeTiltLeft:
                case Lane.RidgeTiltRight:
                    return new[] { 164f + halfWidth * 0.5f, 816f - halfWidth * 0.5f };

                // The 13 m plateau ends in a drop of one cell.
                // The mouth out keeps a portal edge's length from it.
                // The cover is 8.5 m or more from 420 m on.
                case Lane.Staircase:
                    return new[] { Out(700f, -280f, reach), float.NaN };

                // The ravine at 10 m keeps two limits: its piece is a tunnel too, in one tunnel.
                // The walls of the others rise two in one: 8.5 m is 1.75 m out.
                // The west wall of the one at 6 m ends between two posts, 6 m lower 3.5 m out.
                case Lane.Ravines:
                    return new[] {
                        Out(164f, -7f, reach),
                        Out(285.5f, 1.75f, reach),
                        Out(316.5f, -1.75f, reach),
                        Out(360.5f, 3.5f * 3.5f / 6f, reach),
                        Out(387.5f, -1.75f, reach),
                        Out(431.5f, 1.75f, reach),
                        Out(450.5f, -1.75f, reach),
                        Out(566.25f, 1.75f, reach),
                        Out(595.75f, -1.75f, reach),
                        Out(706.75f, 1.75f, reach),
                        Out(735.25f, -1.75f, reach),
                        Out(886f, 7f, reach),
                    };

                case Lane.Gentle:
                    return new[] {
                        Out(220f, -3.5f / 0.15f, reach),
                        Out(725f, 3.5f / 0.08f, reach),
                    };

                case Lane.Hillside:
                    return new float[0];

                case Lane.Twins:
                    return new[] {
                        Out(164f, -7f, reach),
                        Out(277f, 7f, reach),
                        Out(332f, -7f, reach),
                        Out(445f, 7f, reach),
                        Out(507f, -7f, reach),
                        Out(620f, 7f, reach),
                        Out(696f, -7f, reach),
                        Out(809f, 7f, reach),
                    };

                case Lane.Deep:
                    return new[] { Out(164f, -7f, reach), Out(886f, 7f, reach) };

                case Lane.Pit:
                    return new[] { Out(514f, -7f, reach), Out(886f, 7f, reach) };

                case Lane.Sawtooth:
                    var mouths = new float[16];

                    for (var tooth = 0; tooth < 8; tooth++) {
                        mouths[tooth * 2]     = Out(164f + tooth * 84f, -7f, reach);
                        mouths[tooth * 2 + 1] = Out(200f + tooth * 84f, 7f, reach);
                    }

                    return mouths;

                default:
                    return null;
            }
        }

        /// <summary>
        ///     Gets the two mouths of a level road along <see cref="Lane.Ridge" />.
        ///     The flanks rise one in two: a road 1 m higher finds its cover 2 m further in.
        ///     A small road's mouth has 8.5 m there, 7 m out from 12 m and within its reach.
        /// </summary>
        /// <param name="lift">Height of the road over the flat ground.</param>
        /// <param name="cover">Ground cover the mouths have, a small road's by default.</param>
        /// <returns>Distances along the lane of the two mouths.</returns>
        public static float[] RidgeMouths(float lift, float cover = 8.5f) {
            return new[] { 140f + 2f * (cover + lift), 840f - 2f * (cover + lift) };
        }

        /// <summary>
        ///     Gets the middle of the dome at the height of the flat ground.
        /// </summary>
        /// <returns>The world position.</returns>
        public static float3 DomeCentre() {
            return new float3(World(DomePostX), Base, World(DomePostZ));
        }

        /// <summary>
        ///     Gets the area every lane and the dome lie in.
        /// </summary>
        /// <returns>The area, in x and z.</returns>
        public static Bounds2 Area() {
            var last  = FirstPostZ + (LaneCount - 1) * LanePitch;
            var lanes = new Bounds2(
                new float2(World(FirstPostX), World(FirstPostZ) - LaneHalfWidth),
                new float2(World(FirstPostX) + LaneLength, World(last) + LaneHalfWidth));
            var dome  = DomeCentre().xz;

            return lanes | new Bounds2(dome - DomeRadius, dome + DomeRadius);
        }

        /// <summary>
        ///     Gets the height of the ground anywhere on the map, from the formulas.
        ///     The dome is the one relief that is not linear between posts.
        ///     Its flank is straight and wide: the game's interpolation is within millimetres.
        /// </summary>
        /// <param name="position">The x and z of the point.</param>
        /// <returns>The height.</returns>
        public static float Height(float2 position) {
            var u    = position.x - World(FirstPostX);
            var lane = (int)math.round((position.y - World(FirstPostZ)) / (LanePitch * CellSize));
            var v    = position.y - World(FirstPostZ + lane * LanePitch);

            if (lane >= 0
                && lane < LaneCount
                && u >= 0f
                && u <= LaneLength
                && math.abs(v) <= LaneHalfWidth) {
                return Base + Relief((Lane)lane, u, v);
            }

            var radius = math.distance(position, DomeCentre().xz);

            return Base + math.clamp((DomeRadius - radius) * 0.5f, 0f, 42f);
        }

        /// <summary>
        ///     Gets the least ground above a network's left edge, centre, and right edge.
        ///     The measure the tools take at a mouth, on the formulas instead of the heightmap.
        /// </summary>
        /// <param name="position">A point of the network.</param>
        /// <param name="tangent">Direction of the network at that point.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <returns>The ground cover.</returns>
        public static float Cover(float3 position, float3 tangent, float halfWidth) {
            var side   = math.normalizesafe(MathUtils.Right(tangent.xz)) * halfWidth;
            var centre = Height(position.xz);
            var left   = Height(position.xz - side);
            var right  = Height(position.xz + side);

            return math.min(centre, math.min(left, right)) - position.y;
        }

        /// <summary>
        ///     Fills a heightmap, row after row of growing z.
        /// </summary>
        /// <param name="heights">Output: one value for each post.</param>
        public static void Fill(ushort[] heights) {
            var flat = Encode(Base);

            for (var i = 0; i < heights.Length; i++) {
                heights[i] = flat;
            }

            for (var lane = 0; lane < LaneCount; lane++) {
                var middle = FirstPostZ + lane * LanePitch;

                for (var z = -LaneHalfPosts; z <= LaneHalfPosts; z++) {
                    for (var x = 0; x <= LanePosts; x++) {
                        var relief = Relief((Lane)lane, x * CellSize, z * CellSize);

                        heights[(middle + z) * Resolution + FirstPostX + x] = Encode(Base + relief);
                    }
                }
            }

            var reach = (int)(DomeRadius / CellSize);

            for (var z = -reach; z <= reach; z++) {
                for (var x = -reach; x <= reach; x++) {
                    var radius = math.length(new float2(x, z)) * CellSize;
                    var relief = math.clamp((DomeRadius - radius) * 0.5f, 0f, 42f);

                    heights[(DomePostZ + z) * Resolution + DomePostX + x] = Encode(Base + relief);
                }
            }
        }

        /// <summary>
        ///     Gets the height of a lane's ground over the flat ground.
        /// </summary>
        /// <param name="lane">The lane.</param>
        /// <param name="u">Distance from the start of the lane.</param>
        /// <param name="v">Distance to the left of the lane's middle.</param>
        /// <returns>The height, negative in a pit.</returns>
        public static float Relief(Lane lane, float u, float v) {
            switch (lane) {
                case Lane.Ridge:
                    return Ridge(u, 140f, 840f, 42f);

                case Lane.RidgeTiltLeft:
                    return Tilted(u, v * 0.25f);

                case Lane.RidgeTiltRight:
                    return Tilted(u, v * -0.25f);

                case Lane.Staircase:
                    return Staircase(u);

                case Lane.Ravines:
                    return Ravines(u);

                case Lane.Cliff:
                    return u < 210f ? 0f : math.clamp((840f - u) * 0.5f, 0f, 28f);

                case Lane.CliffSkew:
                    return u < 210f + v * math.tan(math.radians(20f))
                        ? 0f
                        : math.clamp((840f - u) * 0.5f, 0f, 28f);

                case Lane.Gentle:
                    return math.clamp(math.min((u - 140f) * 0.15f, (875f - u) * 0.08f), 0f, 30f);

                case Lane.Hillside:
                    return u < 140f || u > 840f ? 0f : math.clamp((v + 10.5f) * 0.35f, 0f, 49f);

                case Lane.Humps:
                    return math.max(
                        math.max(Hump(u, 210f, 28f, 11.5f), Hump(u, 322f, 0f, 12f)),
                        math.max(
                            math.max(Hump(u, 434f, 0f, 13f), Hump(u, 546f, 0f, 14f)),
                            math.max(
                                Hump(u, 658f, 8f, 14f),
                                math.max(Hump(u, 770f, 16f, 14f), Hump(u, 910f, 49f, 14f)))));

                case Lane.Twins:
                    return math.max(
                        math.max(Ridge(u, 140f, 301f, 28f), Ridge(u, 308f, 469f, 28f)),
                        math.max(Ridge(u, 483f, 644f, 28f), Ridge(u, 672f, 833f, 28f)));

                case Lane.Deep:
                    return Ridge(u, 140f, 910f, 70f);

                case Lane.Pit:
                    return Ridge(u, 490f, 910f, 42f) - Ridge(u, 140f, 420f, 10f);

                case Lane.Hipped:
                    return math.clamp(
                        math.min(math.min(u - 140f, 840f - u), LaneHalfWidth - math.abs(v)) * 0.5f,
                        0f,
                        28f);

                case Lane.Sawtooth:
                    return u < 140f || u > 812f ? 0f : Ridge((u - 140f) % 84f, 0f, 84f, 21f);

                case Lane.Plateau:
                    return v < 45f
                        ? Ridge(u, 140f, 840f, 30f)
                        : math.min(Ridge(u, 140f, 840f, 30f), Notch(u, v));

                default:
                    return 0f;
            }
        }

        /// <summary>
        ///     Gets the heightmap value of a height.
        /// </summary>
        /// <param name="height">World height.</param>
        /// <returns>The value the heightmap stores.</returns>
        private static ushort Encode(float height) {
            return (ushort)math.round(height / HeightScale * ushort.MaxValue);
        }

        /// <summary>
        ///     Gets where a post lies in the world: the game puts it at the middle of its cell.
        /// </summary>
        /// <param name="post">Index of the post along x or z.</param>
        /// <returns>The world coordinate.</returns>
        private static float World(int post) {
            return (post + 0.5f - Resolution / 2) * CellSize;
        }

        /// <summary>
        ///     Gets where a mouth goes, out from where the whole width has three limits.
        /// </summary>
        /// <param name="anchor">Where the whole width has three limits, along the lane.</param>
        /// <param name="floor">Distance out to 8.5 m of cover, signed away from the tunnel.</param>
        /// <param name="reach">How far out a mouth may go.</param>
        /// <returns>The distance along the lane of the mouth.</returns>
        private static float Out(float anchor, float floor, float reach) {
            var bound = math.max(reach, 0f);

            return anchor + math.clamp(floor, -bound, bound);
        }

        /// <summary>
        ///     Gets the height of a ridge across a lane, its flanks rising one in two.
        /// </summary>
        /// <param name="u">Distance from the start of the lane.</param>
        /// <param name="start">Where the ridge rises from the flat ground.</param>
        /// <param name="end">Where it falls back to it.</param>
        /// <param name="height">Height of its flat top.</param>
        /// <returns>The height over the flat ground.</returns>
        private static float Ridge(float u, float start, float end, float height) {
            return math.clamp(math.min(u - start, end - u) * 0.5f, 0f, height);
        }

        /// <summary>
        ///     Gets the height of the ridge of <see cref="Lane.Ridge" /> slanting across the lane.
        ///     Only the ridge slants: the flat ground around it stays flat.
        /// </summary>
        /// <param name="u">Distance from the start of the lane.</param>
        /// <param name="tilt">Height the slant adds at this point across the lane.</param>
        /// <returns>The height over the flat ground.</returns>
        private static float Tilted(float u, float tilt) {
            var ridge = Ridge(u, 140f, 840f, 42f);

            return ridge > 0f ? math.max(0f, ridge + tilt) : 0f;
        }

        /// <summary>
        ///     Gets the height of the notch of <see cref="Lane.Plateau" />.
        ///     Its walls rise two in one, and its floor slants across the lane.
        ///     Its floor is 10 m high 60 m to the left of the lane's middle, 12 m 8 m further.
        /// </summary>
        /// <param name="u">Distance from the start of the lane.</param>
        /// <param name="v">Distance to the left of the lane's middle.</param>
        /// <returns>The height over the flat ground.</returns>
        private static float Notch(float u, float v) {
            return 10f + (v - 60f) * 0.25f + math.max(0f, math.abs(u - 650f) - 2f) * 2f;
        }

        /// <summary>
        ///     Gets the height of a hump across a lane, its flanks rising one in two.
        /// </summary>
        /// <param name="u">Distance from the start of the lane.</param>
        /// <param name="centre">Where the middle of its top is.</param>
        /// <param name="top">Length of its flat top.</param>
        /// <param name="height">Height of its top.</param>
        /// <returns>The height over the flat ground.</returns>
        private static float Hump(float u, float centre, float top, float height) {
            var reach = top * 0.5f + height * 2f;

            return math.clamp((reach - math.abs(u - centre)) * 0.5f, 0f, height);
        }

        /// <summary>
        ///     Gets the height of <see cref="Lane.Staircase" />, the plateau under a point.
        /// </summary>
        /// <param name="u">Distance from the start of the lane.</param>
        /// <returns>The height over the flat ground.</returns>
        private static float Staircase(float u) {
            if (u < 140f || u >= 840f) {
                return 0f;
            }

            return s_Plateaus[(int)((u - 140f) / 70f)];
        }

        /// <summary>
        ///     Gets the height of <see cref="Lane.Ravines" />, a ridge less its ravines.
        /// </summary>
        /// <param name="u">Distance from the start of the lane.</param>
        /// <returns>The height over the flat ground.</returns>
        private static float Ravines(float u) {
            var ridge = Ridge(u, 140f, 910f, 42f);

            ridge = math.min(ridge, Ravine(u, 301f, 2f, 10.5f));
            ridge = math.min(ridge, Ravine(u, 374f, 6f, 10.5f));
            ridge = math.min(ridge, Ravine(u, 441f, 0f, 3.5f));
            ridge = math.min(ridge, Ravine(u, 508f, 10f, 10.5f));
            ridge = math.min(ridge, Ravine(u, 581f, 3.5f, 10.5f));

            return math.min(ridge, Ravine(u, 721f, 4.5f, 10.5f));
        }

        /// <summary>
        ///     Gets the height of a ravine across a lane, its walls rising two in one.
        ///     The lane's ground is the least of it and the ridge.
        /// </summary>
        /// <param name="u">Distance from the start of the lane.</param>
        /// <param name="centre">Where the middle of its floor is.</param>
        /// <param name="floor">Height of its floor.</param>
        /// <param name="halfFloor">Half the length of its floor.</param>
        /// <returns>The height over the flat ground.</returns>
        private static float Ravine(float u, float centre, float floor, float halfFloor) {
            return floor + math.max(0f, math.abs(u - centre) - halfFloor) * 2f;
        }
    }
}
