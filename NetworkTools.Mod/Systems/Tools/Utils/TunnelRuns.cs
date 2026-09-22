namespace NetworkTools.Systems.Tools.Utils {
    using Colossal.Mathematics;

    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Tunnel mode of the Connect and Slope tools.
    ///     Finds where to cut a curve so that every tunnel starts and ends at a node deep enough.
    ///     Deep enough means the whole width of the network, not its centre alone.
    /// </summary>
    internal static class TunnelRuns {
        /// <summary>
        ///     Distance between two samples along the curve, about one heightmap pixel.
        /// </summary>
        public const float Step = 4f;

        /// <summary>
        ///     Ground above the curve from which a sample counts as under the terrain.
        /// </summary>
        public const float UnderTerrain = 1f;

        /// <summary>
        ///     A run shorter than this joins the run before it.
        ///     The first run of a curve joins the run after it.
        /// </summary>
        public const float MinRunLength = 16f;

        /// <summary>
        ///     Shortest portal edge and shortest tunnel, in samples.
        /// </summary>
        public const int MinPortalSamples = 2;

        /// <summary>
        ///     Longest portal edge that may be an open cut however deep its far end is.
        ///     The pit of such a short cut is a slot, not a canyon.
        /// </summary>
        public const float MaxDeepCutLength = 32f;

        /// <summary>
        ///     Ground cover, in elevation limits, from which an existing node can be a mouth.
        ///     A tool that cuts existing edges cannot place a node close to another one.
        ///     The node there is the mouth when it has nearly the cover of one.
        /// </summary>
        public const float NearMouthLimits = 2.5f;

        /// <summary>
        ///     Tolerance on the ground cover of a node that is a mouth.
        ///     A node placed for a mouth has the threshold exactly.
        /// </summary>
        public const float Tolerance = 0.5f;

        /// <summary>
        ///     Gets the ground cover from which an existing node can be a mouth.
        ///     The cut of an edge and the mouth system must agree on it.
        /// </summary>
        /// <param name="limit">Elevation limit of the network.</param>
        /// <returns>The cover the node needs on its worst side.</returns>
        public static float NearMouthDepth(float limit) {
            return limit * NearMouthLimits - Tolerance;
        }

        /// <summary>
        ///     Gets the number of samples taken along a curve, both ends included.
        /// </summary>
        /// <param name="length">Length of the curve.</param>
        /// <returns>The sample count, at least two.</returns>
        public static int SampleCount(float length) {
            return math.max(2, (int)math.ceil(length / Step) + 1);
        }

        /// <summary>
        ///     Marks the samples that belong to a run under the terrain.
        ///     Each run is widened by one sample, to start and end on a sample at the surface.
        /// </summary>
        /// <param name="depth">Ground above the curve at each sample.</param>
        /// <param name="length">Length of the curve.</param>
        /// <param name="under">Output: true for each sample of a run under the terrain.</param>
        public static void MarkUnder(
            NativeArray<float> depth,
            float              length,
            NativeArray<bool>  under) {
            var samples = depth.Length;
            var raw     = new NativeArray<bool>(samples, Allocator.Temp);

            for (var i = 0; i < samples; i++) {
                raw[i] = depth[i] > UnderTerrain;
            }

            // A run that is too short takes the state of the run before it.
            var minRunSamples = (int)math.ceil(MinRunLength / (length / (samples - 1)));
            var runStart      = 0;

            for (var i = 1; i <= samples; i++) {
                if (i < samples && raw[i] == raw[runStart]) {
                    continue;
                }

                if (i - runStart < minRunSamples && runStart > 0) {
                    for (var j = runStart; j < i; j++) {
                        raw[j] = raw[runStart - 1];
                    }
                }

                runStart = i;
            }

            // The first run has none before it: too short, it takes the state of the run after.
            var firstEnd = 1;

            while (firstEnd < samples && raw[firstEnd] == raw[0]) {
                firstEnd++;
            }

            if (firstEnd < minRunSamples && firstEnd < samples) {
                for (var j = 0; j < firstEnd; j++) {
                    raw[j] = raw[firstEnd];
                }
            }

            for (var i = 0; i < samples; i++) {
                under[i] = raw[i] || (i > 0 && raw[i - 1]) || (i < samples - 1 && raw[i + 1]);
            }

            raw.Dispose();
        }

        /// <summary>
        ///     Splits a curve into its runs, in order: on the ground, open cut, or tunnel.
        ///     <see cref="MarkUnder" /> finds the runs under the terrain.
        ///     Each of those is split again into its tunnels and the open cuts around them.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="bezier">The curve to split.</param>
        /// <param name="length">Length of the curve.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <param name="limit">Elevation limit of the network prefab.</param>
        /// <param name="runs">Output: the curve positions of each run, the last ends at 1.</param>
        public static void Split(
            ref TerrainHeightData   terrain,
            Bezier4x3               bezier,
            float                   length,
            float                   halfWidth,
            float                   limit,
            ref NativeList<Bounds1> runs) {
            var samples = SampleCount(length);
            var depth   = new NativeArray<float>(samples, Allocator.Temp);
            var under   = new NativeArray<bool>(samples, Allocator.Temp);

            for (var i = 0; i < samples; i++) {
                var position = MathUtils.Position(bezier, i / (float)(samples - 1));

                depth[i] = TerrainUtils.SampleHeight(ref terrain, position) - position.y;
            }

            MarkUnder(depth, length, under);

            // The piece between two samples is under the terrain when both samples are.
            var step       = 1f / (samples - 1);
            var dipSamples = (int)math.ceil(halfWidth * 2f / (length * step));
            var runStart   = 0;

            for (var i = 1; i < samples; i++) {
                var isUnder = under[runStart] && under[runStart + 1];

                if (i < samples - 1 && (under[i] && under[i + 1]) == isUnder) {
                    continue;
                }

                if (isUnder) {
                    SplitUnder(
                        ref terrain,
                        bezier,
                        halfWidth,
                        limit,
                        runStart,
                        i,
                        step,
                        dipSamples,
                        ref runs);
                } else {
                    runs.Add(new Bounds1(runStart * step, i * step));
                }

                runStart = i;
            }

            // The last sample sits at position 1, give or take the rounding.
            var last = runs[runs.Length - 1];

            last.max              = 1f;
            runs[runs.Length - 1] = last;

            depth.Dispose();
            under.Dispose();
        }

        /// <summary>
        ///     Cuts a curve into the runs of <see cref="Split" />, one course each.
        ///     The vanilla course solver reads the endpoint elevations a course declares.
        ///     One of at least the limit keeps the course at or above its straight line.
        ///     One of at most minus the limit keeps it at or below that line.
        ///     A course declaring one of each lies on the line whatever the ground does.
        ///     The network is built with elevations measured on the ground afterwards.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="curve">The curve to cut.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <param name="limit">Elevation limit of the network prefab.</param>
        /// <param name="cutFlags">Course position flags of the node a cut makes.</param>
        /// <param name="split">Output: the runs are added to this list.</param>
        public static void SplitAtGrade(
            ref TerrainHeightData      terrain,
            EdgeConfig                 curve,
            float                      halfWidth,
            float                      limit,
            CoursePosFlags             cutFlags,
            ref NativeList<EdgeConfig> split) {
            var runs = new NativeList<Bounds1>(8, Allocator.Temp);

            Split(ref terrain, curve.Bezier, curve.Length, halfWidth, limit, ref runs);

            for (var i = 0; i < runs.Length; i++) {
                AddRun(curve, runs[i].min, runs[i].max, limit, cutFlags, ref split);
            }

            runs.Dispose();
        }

        /// <summary>
        ///     Adds one run of a curve as a course that lies on its straight line.
        ///     A run that does not reach an end of the curve gets a new node there.
        /// </summary>
        /// <param name="curve">The curve the run is cut from.</param>
        /// <param name="from">Curve position where the run starts.</param>
        /// <param name="to">Curve position where the run ends.</param>
        /// <param name="limit">Elevation limit of the network prefab.</param>
        /// <param name="cutFlags">Course position flags of the node a cut makes.</param>
        /// <param name="split">Output: the run is added to this list.</param>
        public static void AddRun(
            EdgeConfig                 curve,
            float                      from,
            float                      to,
            float                      limit,
            CoursePosFlags             cutFlags,
            ref NativeList<EdgeConfig> split) {
            var run = curve;

            run.Bezier = MathUtils.Cut(curve.Bezier, new Bounds1(from, to));
            run.Length = MathUtils.Length(run.Bezier);

            if (from > 0f) {
                run.StartNodeEntity = Entity.Null;
                run.StartNodeFlags  = cutFlags;
            }

            if (to < 1f) {
                run.EndNodeEntity = Entity.Null;
                run.EndNodeFlags  = cutFlags;
            }

            run.StartNodeElevation = limit;
            run.EndNodeElevation   = -limit;
            split.Add(run);
        }

        /// <summary>
        ///     Splits a run under the terrain into tunnels and the open cuts around them.
        ///     A tunnel starts and ends where the whole width has three limits of ground over it.
        ///     With less on one side, the game leaves a gap beside the head wall.
        ///     With more, the game digs a pit in front of the mouth.
        ///     An open cut keeps no ground above that depth.
        ///     A tunnel stops before a dip that ends it (<see cref="IsDip" />).
        ///     Another tunnel starts after the dip.
        ///     A mouth keeps <see cref="MinPortalSamples" /> samples from the ends of the run.
        ///     It keeps as many from the mouth before.
        ///     A tunnel runs to an end of the curve that is deep enough.
        ///     Nearly is enough there, by <see cref="Tolerance" />.
        ///     The tunnel goes on in the next curve, or the node there is the mouth.
        ///     Whatever is left of the run stays an open cut.
        ///     That is all of it when the curve runs along a hillside.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="bezier">The curve the run belongs to.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <param name="limit">Elevation limit of the network prefab.</param>
        /// <param name="first">First sample of the run.</param>
        /// <param name="last">Last sample of the run.</param>
        /// <param name="step">Curve position between two samples.</param>
        /// <param name="dipSamples">Samples across the width, the least a dip needs.</param>
        /// <param name="runs">Output: the runs found are added to this list.</param>
        private static void SplitUnder(
            ref TerrainHeightData   terrain,
            Bezier4x3               bezier,
            float                   halfWidth,
            float                   limit,
            int                     first,
            int                     last,
            float                   step,
            int                     dipSamples,
            ref NativeList<Bounds1> runs) {
            var deep = limit * 3f;

            // An end of the curve is a node already, and with nearly the cover it can be a mouth.
            var deepEnd = deep - Tolerance;

            // Outside Burst a product can compare unequal to itself, kept at two precisions.
            var tiny = step * 0.01f;
            var from = first * step;
            var i    = first;

            while (i < last) {
                // Mouth in: the first point deep enough, a portal edge away from the run before.
                var a       = i;
                var mouthIn = i * step;

                if (Cover(ref terrain, bezier, mouthIn, halfWidth) < deepEnd) {
                    var nearest = (int)math.ceil(from / step - 0.001f) + MinPortalSamples;

                    a = math.max(i, nearest);

                    while (a < last && Cover(ref terrain, bezier, a * step, halfWidth) < deep) {
                        a++;
                    }

                    if (a >= last) {
                        break;
                    }

                    mouthIn = a == nearest
                        ? a * step
                        : Mouth(ref terrain, bezier, halfWidth, deep, (a - 1) * step, a * step);
                }

                // The tunnel goes on to the sample before a dip, or to the end of the run.
                var b = a;

                while (b < last
                       && !IsDip(
                           ref terrain,
                           bezier,
                           halfWidth,
                           limit,
                           b + 1,
                           last,
                           step,
                           dipSamples)) {
                    b++;
                }

                // Mouth out: the last point deep enough, a portal edge away from the run's end.
                var mouthOut = last * step;

                if (b < last || Cover(ref terrain, bezier, mouthOut, halfWidth) < deepEnd) {
                    var nearest = last - MinPortalSamples;
                    var c       = math.min(b, nearest);

                    while (c > a && Cover(ref terrain, bezier, c * step, halfWidth) < deep) {
                        c--;
                    }

                    mouthOut = c == nearest || c <= a
                        ? c * step
                        : Mouth(ref terrain, bezier, halfWidth, deep, (c + 1) * step, c * step);
                }

                if (mouthOut - mouthIn >= MinPortalSamples * step) {
                    if (mouthIn - from > tiny) {
                        runs.Add(new Bounds1(from, mouthIn));
                    }

                    runs.Add(new Bounds1(mouthIn, mouthOut));
                    from = mouthOut;
                }

                i = math.min(b + 1, last);
            }

            if (last * step - from > tiny) {
                runs.Add(new Bounds1(from, last * step));
            }
        }

        /// <summary>
        ///     Checks whether a dip of the ground ends a tunnel at a sample.
        ///     A dip under one limit ends it however short: the roof would show.
        ///     One under three limits ends it when it is at least the width long.
        ///     The game cuts a course there itself, its ends on its own samples.
        ///     Cut here instead, both its ends are mouths.
        ///     A dip that keeps two limits stays a tunnel then.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="bezier">The curve the run belongs to.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <param name="limit">Elevation limit of the network prefab.</param>
        /// <param name="sample">The sample.</param>
        /// <param name="last">Last sample of the run.</param>
        /// <param name="step">Curve position between two samples.</param>
        /// <param name="dipSamples">Samples across the width, the least a dip needs.</param>
        /// <returns>True if the sample is under a dip that ends the tunnel.</returns>
        private static bool IsDip(
            ref TerrainHeightData terrain,
            Bezier4x3             bezier,
            float                 halfWidth,
            float                 limit,
            int                   sample,
            int                   last,
            float                 step,
            int                   dipSamples) {
            var cover = Cover(ref terrain, bezier, sample * step, halfWidth);

            if (cover < limit) {
                return true;
            }

            if (cover >= limit * 3f) {
                return false;
            }

            var count = 1;

            while (count < dipSamples
                   && sample + count <= last
                   && Cover(ref terrain, bezier, (sample + count) * step, halfWidth) < limit * 3f) {
                count++;
            }

            return count >= dipSamples;
        }

        /// <summary>
        ///     Finds the curve position from which the ground cover is enough for a tunnel.
        ///     Searches between a shallow point and a deep point.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="bezier">The curve.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <param name="tunnelDepth">Ground cover a tunnel needs.</param>
        /// <param name="shallow">Curve position with less cover than that.</param>
        /// <param name="deep">Curve position with at least that cover.</param>
        /// <returns>The curve position of the mouth.</returns>
        private static float Mouth(
            ref TerrainHeightData terrain,
            Bezier4x3             bezier,
            float                 halfWidth,
            float                 tunnelDepth,
            float                 shallow,
            float                 deep) {
            for (var i = 0; i < 6; i++) {
                var middle = (shallow + deep) * 0.5f;

                if (Cover(ref terrain, bezier, middle, halfWidth) < tunnelDepth) {
                    shallow = middle;
                } else {
                    deep = middle;
                }
            }

            return deep;
        }

        /// <summary>
        ///     Finds the first curve position with the cover of a mouth, going from one to another.
        ///     A node moved along its network to be a mouth goes there.
        ///     The runs of one edge do not tell where that is.
        ///     They keep a mouth a portal edge away from the start of the edge.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="bezier">The curve.</param>
        /// <param name="length">Length of the curve.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <param name="limit">Elevation limit of the network prefab.</param>
        /// <param name="from">Curve position to search from.</param>
        /// <param name="to">Curve position to search up to, on either side of the first.</param>
        /// <returns>The curve position of the mouth, negative when there is none.</returns>
        public static float FirstMouth(
            ref TerrainHeightData terrain,
            Bezier4x3             bezier,
            float                 length,
            float                 halfWidth,
            float                 limit,
            float                 from,
            float                 to) {
            var deep     = limit * 3f;
            var steps    = math.max(1, (int)math.ceil(math.abs(to - from) * length));
            var previous = from;

            for (var i = 0; i <= steps; i++) {
                var t = math.lerp(from, to, i / (float)steps);

                if (Cover(ref terrain, bezier, t, halfWidth) >= deep) {
                    return i == 0 ? t : Mouth(ref terrain, bezier, halfWidth, deep, previous, t);
                }

                previous = t;
            }

            return -1f;
        }

        /// <summary>
        ///     Gets the least ground above the network's left edge, centre, and right edge.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="bezier">The curve.</param>
        /// <param name="t">Curve position to measure at.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <returns>The ground cover, negative when the network is above the ground.</returns>
        public static float Cover(
            ref TerrainHeightData terrain,
            Bezier4x3             bezier,
            float                 t,
            float                 halfWidth) {
            var position = MathUtils.Position(bezier, t);
            var side     = Side(bezier, t, halfWidth);
            var centre   = TerrainUtils.SampleHeight(ref terrain, position);
            var left     = TerrainUtils.SampleHeight(ref terrain, position - side);
            var right    = TerrainUtils.SampleHeight(ref terrain, position + side);

            return math.min(centre, math.min(left, right)) - position.y;
        }

        /// <summary>
        ///     Gets the height over the ground of the network's left and right edges.
        ///     This is what the game stores as the elevation of a network it lays.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="bezier">The curve.</param>
        /// <param name="t">Curve position to measure at.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <returns>The left and right elevations, negative under the ground.</returns>
        public static float2 Elevation(
            ref TerrainHeightData terrain,
            Bezier4x3             bezier,
            float                 t,
            float                 halfWidth) {
            var position = MathUtils.Position(bezier, t);
            var side     = Side(bezier, t, halfWidth);
            var left     = TerrainUtils.SampleHeight(ref terrain, position - side);
            var right    = TerrainUtils.SampleHeight(ref terrain, position + side);

            return position.y - new float2(left, right);
        }

        /// <summary>
        ///     Checks whether the game lets a network go under the ground, as its net tool does.
        ///     A bridge, a quay, a pier, or a waterway cannot.
        ///     A tool lays such a network as it does without Tunnel mode.
        /// </summary>
        /// <param name="placeable">The placement data of the network prefab.</param>
        /// <returns>True if the network can be a tunnel.</returns>
        public static bool CanTunnel(PlaceableNetData placeable) {
            return placeable.m_ElevationRange.min < 0f;
        }

        /// <summary>
        ///     Gets the elevation to store on a curve that is written in place.
        ///     Under the ground it is the measured one, and within the limit there is none.
        ///     Above the limit the curve keeps what it had:
        ///     a bridge stays one, and a ground road is not made one.
        /// </summary>
        /// <param name="measured">Elevation measured on the terrain.</param>
        /// <param name="existing">Elevation the entity has now.</param>
        /// <param name="limit">Elevation limit of the network prefab.</param>
        /// <returns>The elevation to store.</returns>
        public static float2 Stored(float2 measured, float2 existing, float limit) {
            var above = math.select(default, math.max(existing, 0f), measured >= limit);

            return math.select(above, measured, measured <= -limit);
        }

        /// <summary>
        ///     Gets the offset from the centre of the network to its right edge.
        /// </summary>
        /// <param name="bezier">The curve.</param>
        /// <param name="t">Curve position.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <returns>The horizontal offset to the right edge.</returns>
        private static float3 Side(Bezier4x3 bezier, float t, float halfWidth) {
            var right = MathUtils.Right(MathUtils.Tangent(bezier, t).xz);

            return new float3 {
                xz = math.normalizesafe(right) * halfWidth
            };
        }
    }
}
