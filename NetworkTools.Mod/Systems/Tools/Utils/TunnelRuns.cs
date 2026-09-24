namespace NetworkTools.Systems.Tools.Utils {
    using Colossal.Mathematics;

    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Tunnel mode of the Connect, Slope, and Parallel tools.
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
        private const float UnderTerrain = 1f;

        /// <summary>
        ///     Shortest stretch on or under the ground.
        ///     A shorter one joins the stretch before it.
        ///     The first stretch of a curve joins the one after it instead.
        /// </summary>
        private const float MinStretchLength = 16f;

        /// <summary>
        ///     Shortest portal edge and shortest tunnel, in samples.
        /// </summary>
        public const int MinPortalSamples = 2;

        /// <summary>
        ///     Longest portal edge that may be an open cut however deep its middle is.
        ///     The pit of such a short cut is a slot, not a canyon.
        /// </summary>
        public const float MaxDeepCutLength = 32f;

        /// <summary>
        ///     Margin on the ground cover of a mouth and on the step of its head wall.
        ///     A node placed for a mouth keeps it in hand.
        ///     A node that exists already may take it as slack.
        ///     Every node a tool placed then passes as a mouth, whatever the float precision.
        /// </summary>
        public const float Tolerance = 0.5f;

        /// <summary>
        ///     Gets how far out a mouth may go from where the whole width has three limits.
        ///     The game makes a course of its own of a stretch under three limits the width long.
        ///     Its node goes on a sample of the game's: one sample short keeps clear of it.
        /// </summary>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <returns>The distance, zero or less when a mouth cannot move.</returns>
        public static float Reach(float halfWidth) {
            return halfWidth * 2f - Step;
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
        ///     Marks the samples that belong to a stretch under the terrain.
        ///     It widens each stretch by one sample, to start and end on a sample at the surface.
        /// </summary>
        /// <param name="depth">Ground above the curve at each sample.</param>
        /// <param name="length">Length of the curve.</param>
        /// <param name="under">Output: true for each sample of a stretch under the terrain.</param>
        private static void MarkUnder(
            NativeArray<float> depth,
            float              length,
            NativeArray<bool>  under) {
            var samples = depth.Length;
            var raw     = new NativeArray<bool>(samples, Allocator.Temp);

            for (var i = 0; i < samples; i++) {
                raw[i] = depth[i] > UnderTerrain;
            }

            // A stretch that is too short takes the state of the one before it.
            var minStretchSamples = (int)math.ceil(MinStretchLength / (length / (samples - 1)));
            var stretchStart      = 0;

            for (var i = 1; i <= samples; i++) {
                if (i < samples && raw[i] == raw[stretchStart]) {
                    continue;
                }

                if (i - stretchStart < minStretchSamples && stretchStart > 0) {
                    for (var j = stretchStart; j < i; j++) {
                        raw[j] = raw[stretchStart - 1];
                    }
                }

                stretchStart = i;
            }

            // The first stretch has none before it: too short, it takes the state of the next.
            var firstEnd = 1;

            while (firstEnd < samples && raw[firstEnd] == raw[0]) {
                firstEnd++;
            }

            if (firstEnd < minStretchSamples && firstEnd < samples) {
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
        ///     <see cref="MarkUnder" /> finds the stretches under the terrain.
        ///     <see cref="SplitUnder" /> finds the tunnels in each, and the open cuts around them.
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
            var step         = 1f / (samples - 1);
            var dipSamples   = (int)math.ceil(halfWidth * 2f / (length * step));
            var stretchStart = 0;

            for (var i = 1; i < samples; i++) {
                var isUnder = under[stretchStart] && under[stretchStart + 1];

                if (i < samples - 1 && (under[i] && under[i + 1]) == isUnder) {
                    continue;
                }

                if (isUnder) {
                    SplitUnder(
                        ref terrain,
                        bezier,
                        halfWidth,
                        limit,
                        stretchStart,
                        i,
                        step,
                        dipSamples,
                        ref runs);
                } else {
                    runs.Add(new Bounds1(stretchStart * step, i * step));
                }

                stretchStart = i;
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
        ///     The game builds the network with elevations measured on the ground afterwards.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="curve">The curve to cut.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <param name="limit">Elevation limit of the network prefab.</param>
        /// <param name="cutFlags">Course position flags of the node a cut makes.</param>
        /// <param name="split">Output: the list that receives the runs.</param>
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
        /// <param name="split">Output: the list that receives the run.</param>
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
        ///     Splits a stretch under the terrain into tunnels and the open cuts around them.
        ///     A tunnel starts and ends where the whole width has three limits of ground over it.
        ///     With less on one side, the game leaves a gap beside the head wall.
        ///     With more, the game digs a pit in front of the mouth.
        ///     An open cut keeps no ground above that depth.
        ///     A tunnel stops before a dip that ends it (<see cref="IsDip" />).
        ///     Another tunnel starts after the dip.
        ///     A mouth keeps <see cref="MinPortalSamples" /> samples from the ends of the stretch.
        ///     It keeps as many from the mouth before.
        ///     A tunnel is as long at least, once its mouths moved out, or it stays an open cut.
        ///     A tunnel runs to an end of the curve that can be a mouth, by <see cref="IsMouth" />.
        ///     The tunnel goes on in the next curve, or the node there is the mouth.
        ///     Whatever is left of the stretch stays an open cut.
        ///     That is all of it when the curve runs along a hillside.
        ///     The mouths then move out into the open cuts (<see cref="MoveMouthsOut" />).
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="bezier">The curve the stretch belongs to.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <param name="limit">Elevation limit of the network prefab.</param>
        /// <param name="first">First sample of the stretch.</param>
        /// <param name="last">Last sample of the stretch.</param>
        /// <param name="step">Curve position between two samples.</param>
        /// <param name="dipSamples">Samples across the width, the least a dip needs.</param>
        /// <param name="runs">Output: the list that receives the runs found.</param>
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

            // Outside Burst a product can compare unequal to itself, kept at two precisions.
            var tiny        = step * 0.01f;
            var from        = first * step;
            var i           = first;
            var firstRun    = runs.Length;
            var tunnelFirst = false;

            while (i < last) {
                // The mouth in is the first point deep enough.
                // It keeps a portal edge's length from the run before.
                // An end of the curve is a node already: a tunnel runs to one that can be a mouth.
                // After a dip, the tunnel keeps a portal edge's length from the one before.
                var a       = i;
                var mouthIn = i * step;

                if (i > first
                    || !IsMouth(ref terrain, bezier, mouthIn, halfWidth, limit, Tolerance)) {
                    var nearest = (int)math.ceil(from / step - 0.001f) + MinPortalSamples;

                    a = math.max(i, nearest);

                    // The stretch can end deep only where the curve ends, inside a hill.
                    while (a <= last && Cover(ref terrain, bezier, a * step, halfWidth) < deep) {
                        a++;
                    }

                    if (a > last) {
                        break;
                    }

                    mouthIn = a == nearest
                        ? a * step
                        : Mouth(ref terrain, bezier, halfWidth, deep, (a - 1) * step, a * step);
                }

                // The tunnel goes on to the sample before a dip, or to the end of the stretch.
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

                // The mouth out is the last point deep enough.
                // It keeps a portal edge's length from the end of the stretch.
                var mouthOut = last * step;

                if (b < last
                    || !IsMouth(ref terrain, bezier, mouthOut, halfWidth, limit, Tolerance)) {
                    var nearest = last - MinPortalSamples;
                    var c       = math.min(b, nearest);

                    // A tunnel from the start of the curve may have three limits there alone.
                    while (c >= a && Cover(ref terrain, bezier, c * step, halfWidth) < deep) {
                        c--;
                    }

                    mouthOut = c == nearest || c < a
                        ? c * step
                        : Mouth(ref terrain, bezier, halfWidth, deep, (c + 1) * step, c * step);
                }

                if (mouthOut - mouthIn > tiny) {
                    if (mouthIn - from > tiny) {
                        runs.Add(new Bounds1(from, mouthIn));
                    } else if (runs.Length == firstRun) {
                        // No cut before the first tunnel: the runs start with it.
                        tunnelFirst = true;
                    }

                    runs.Add(new Bounds1(mouthIn, mouthOut));
                    from = mouthOut;
                }

                i = math.min(b + 1, last);
            }

            if (last * step - from > tiny) {
                runs.Add(new Bounds1(from, last * step));
            }

            MoveMouthsOut(
                ref terrain,
                bezier,
                halfWidth,
                limit,
                step,
                firstRun,
                tunnelFirst,
                ref runs);
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
        /// <param name="bezier">The curve the stretch belongs to.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <param name="limit">Elevation limit of the network prefab.</param>
        /// <param name="sample">The sample.</param>
        /// <param name="last">Last sample of the stretch.</param>
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
        ///     Moves each mouth of a stretch under the terrain out into the open cut it faces.
        ///     <see cref="SplitUnder" /> puts the mouths where the whole width has three limits.
        ///     The game makes a tunnel of an edge with less at its ends (<see cref="IsTunnel" />).
        ///     A mouth nearer the surface spares a deep open cut.
        ///     A cut between two tunnels that the game makes a tunnel too keeps its mouths.
        ///     The dip under it keeps two limits, and moved, they would open a slot over it.
        ///     The two mouths of any other cut between two tunnels share it.
        ///     They keep a portal edge's length between them, as between a mouth and the surface.
        ///     A tunnel left with three limits nowhere the game reads them gets one mouth back.
        ///     The one that moved less goes back to its anchor (<see cref="KeepsThreeLimits" />).
        ///     A tunnel still shorter than a portal edge is none: it joins the cuts around it.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="bezier">The curve the runs belong to.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <param name="limit">Elevation limit of the network prefab.</param>
        /// <param name="step">Curve position between two samples.</param>
        /// <param name="first">Index of the stretch's first run.</param>
        /// <param name="tunnelFirst">True if that run is a tunnel, not an open cut.</param>
        /// <param name="runs">The runs of the curve, their ends moved in place.</param>
        private static void MoveMouthsOut(
            ref TerrainHeightData   terrain,
            Bezier4x3               bezier,
            float                   halfWidth,
            float                   limit,
            float                   step,
            int                     first,
            bool                    tunnelFirst,
            ref NativeList<Bounds1> runs) {
            var portal  = MinPortalSamples * step;
            var anchors = new NativeArray<Bounds1>(runs.Length - first, Allocator.Temp);

            for (var r = first; r < runs.Length; r++) {
                anchors[r - first] = runs[r];
            }

            // An open cut and a tunnel take turns: every other run is a cut.
            var cutParity = tunnelFirst ? 1 : 0;

            for (var r = first + cutParity; r < runs.Length; r += 2) {
                var before = r > first;
                var after  = r + 1 < runs.Length;
                var cut    = runs[r];
                var middle = MathUtils.Center(cut);

                if (before && after && IsTunnel(ref terrain, bezier, halfWidth, limit, cut)) {
                    continue;
                }

                if (before) {
                    var bound = after ? middle - portal * 0.5f : cut.max - portal;
                    var mouth = MoveOut(
                        ref terrain,
                        bezier,
                        halfWidth,
                        limit,
                        cut.min,
                        1f,
                        bound);

                    runs[r - 1] = new Bounds1(runs[r - 1].min, mouth);
                    cut.min     = mouth;
                }

                if (after) {
                    var bound = before ? middle + portal * 0.5f : cut.min + portal;
                    var mouth = MoveOut(
                        ref terrain,
                        bezier,
                        halfWidth,
                        limit,
                        cut.max,
                        -1f,
                        bound);

                    runs[r + 1] = new Bounds1(mouth, runs[r + 1].max);
                    cut.max     = mouth;
                }

                runs[r] = cut;
            }

            // With both mouths out, a tunnel may have three limits nowhere the game reads them.
            // The mouth that moved less goes back to its anchor: the tunnel has them there.
            for (var r = first + 1 - cutParity; r < runs.Length; r += 2) {
                var run         = runs[r];
                var anchor      = anchors[r - first];
                var startStayed = run.min >= anchor.min;
                var endStayed   = run.max <= anchor.max;

                if (KeepsThreeLimits(
                        ref terrain,
                        bezier,
                        halfWidth,
                        limit,
                        run,
                        startStayed,
                        endStayed)) {
                    continue;
                }

                // A move is measured on the ground: the curve may run faster at one mouth.
                // Moves as long as each other, give or take the rounding, send the start back.
                var startMoved = MathUtils.Length(bezier.xz, new Bounds1(run.min, anchor.min));
                var endMoved   = MathUtils.Length(bezier.xz, new Bounds1(anchor.max, run.max));
                var rounding   = Step * 0.01f;

                if (!startStayed && (endStayed || startMoved <= endMoved + rounding)) {
                    runs[r - 1] = new Bounds1(runs[r - 1].min, anchor.min);
                    runs[r]     = new Bounds1(anchor.min, run.max);
                } else if (!endStayed) {
                    runs[r + 1] = new Bounds1(anchor.max, runs[r + 1].max);
                    runs[r]     = new Bounds1(run.min, anchor.max);
                }
            }

            anchors.Dispose();

            // Outside Burst a difference can come out just under a portal edge's length.
            var shortest = portal - step * 0.01f;
            var kept     = first;
            var lastCut  = false;

            for (var r = first; r < runs.Length; r++) {
                var run   = runs[r];
                var isCut = (r - first) % 2 == cutParity || MathUtils.Size(run) < shortest;

                if (isCut && lastCut) {
                    runs[kept - 1] = new Bounds1(runs[kept - 1].min, run.max);
                } else {
                    runs[kept] = run;
                    kept++;
                }

                lastCut = isCut;
            }

            runs.Length = kept;
        }

        /// <summary>
        ///     Moves a mouth out from where the whole width has three limits, by steps.
        ///     It goes to the outermost point that passes <see cref="IsMouth" />, placed.
        ///     It skips a point where only the head wall has a step.
        ///     One with too little cover ends the search: the point where it is enough is the last.
        ///     It goes no further than <see cref="Reach" />, nor past the bound.
        ///     A step covers <see cref="Step" /> on the ground at the anchor.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="bezier">The curve.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <param name="limit">Elevation limit of the network prefab.</param>
        /// <param name="anchor">Curve position where the whole width has three limits.</param>
        /// <param name="direction">1 towards the end of the curve, -1 towards its start.</param>
        /// <param name="bound">Curve position the mouth may reach, itself a candidate.</param>
        /// <returns>The curve position of the mouth, the anchor when it cannot move.</returns>
        private static float MoveOut(
            ref TerrainHeightData terrain,
            Bezier4x3             bezier,
            float                 halfWidth,
            float                 limit,
            float                 anchor,
            float                 direction,
            float                 bound) {
            if ((bound - anchor) * direction <= 0f) {
                return anchor;
            }

            var spacing  = direction * Step / math.length(MathUtils.Tangent(bezier, anchor).xz);
            var count    = (int)(Reach(halfWidth) / Step);
            var least    = limit * 2f + Tolerance;
            var mouth    = anchor;
            var previous = anchor;

            for (var k = 1; k <= count; k++) {
                var candidate = anchor + k * spacing;
                var atBound   = (candidate - bound) * spacing >= 0f;

                if (atBound) {
                    candidate = bound;
                }

                if (Cover(ref terrain, bezier, candidate, halfWidth) < least) {
                    var edge = Mouth(ref terrain, bezier, halfWidth, least, candidate, previous);

                    return IsMouth(ref terrain, bezier, edge, halfWidth, limit, 0f) ? edge : mouth;
                }

                if (IsMouth(ref terrain, bezier, candidate, halfWidth, limit, 0f)) {
                    mouth = candidate;
                }

                if (atBound) {
                    break;
                }

                previous = candidate;
            }

            return mouth;
        }

        /// <summary>
        ///     Checks whether the game makes a tunnel of a run, as it does of an edge.
        ///     The deeper side needs two limits at both ends and at the middle.
        ///     The shallower side needs three limits at one of them.
        ///     Nearly is enough, by <see cref="Tolerance" />.
        ///     A run judged a tunnel keeps its mouths where they are.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="bezier">The curve the run belongs to.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <param name="limit">Elevation limit of the network prefab.</param>
        /// <param name="run">Curve positions of the run.</param>
        /// <returns>True if the run would be a tunnel.</returns>
        private static bool IsTunnel(
            ref TerrainHeightData terrain,
            Bezier4x3             bezier,
            float                 halfWidth,
            float                 limit,
            Bounds1               run) {
            var start     = Elevation(ref terrain, bezier, run.min, halfWidth);
            var middle    = Elevation(ref terrain, bezier, MathUtils.Center(run), halfWidth);
            var end       = Elevation(ref terrain, bezier, run.max, halfWidth);
            var deeper    = new float3(math.cmin(start), math.cmin(middle), math.cmin(end));
            var shallower = new float3(math.cmax(start), math.cmax(middle), math.cmax(end));

            return math.cmax(deeper) <= Tolerance - limit * 2f
                   && math.cmin(shallower) <= Tolerance - limit * 3f;
        }

        /// <summary>
        ///     Checks whether the game still finds three limits on a tunnel whose mouths moved out.
        ///     It reads the shallower side at the start, the middle, and the end of the edge.
        ///     A moved mouth has less: an end counts when it stayed, an anchor has them exactly.
        ///     Nearly is enough there, by <see cref="Tolerance" />.
        ///     The middle keeps it in hand instead, as a mouth placed does.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="bezier">The curve the run belongs to.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <param name="limit">Elevation limit of the network prefab.</param>
        /// <param name="run">Curve positions of the tunnel.</param>
        /// <param name="startStayed">True if the start of the tunnel did not move out.</param>
        /// <param name="endStayed">True if the end of the tunnel did not move out.</param>
        /// <returns>True if the game finds three limits on the tunnel.</returns>
        private static bool KeepsThreeLimits(
            ref TerrainHeightData terrain,
            Bezier4x3             bezier,
            float                 halfWidth,
            float                 limit,
            Bounds1               run,
            bool                  startStayed,
            bool                  endStayed) {
            var nearly = limit * 3f - Tolerance;
            var inHand = limit * 3f + Tolerance;
            var middle = MathUtils.Center(run);

            return (startStayed && Cover(ref terrain, bezier, run.min, halfWidth) >= nearly)
                   || (endStayed && Cover(ref terrain, bezier, run.max, halfWidth) >= nearly)
                   || Cover(ref terrain, bezier, middle, halfWidth) >= inHand;
        }

        /// <summary>
        ///     Finds the curve position from which the ground cover reaches a threshold.
        ///     Searches between a shallow point and a deep point.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="bezier">The curve.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <param name="threshold">Ground cover to reach.</param>
        /// <param name="shallow">Curve position with less cover than that.</param>
        /// <param name="deep">Curve position with at least that cover.</param>
        /// <returns>The curve position, on the deep side.</returns>
        private static float Mouth(
            ref TerrainHeightData terrain,
            Bezier4x3             bezier,
            float                 halfWidth,
            float                 threshold,
            float                 shallow,
            float                 deep) {
            for (var i = 0; i < 6; i++) {
                var middle = (shallow + deep) * 0.5f;

                if (Cover(ref terrain, bezier, middle, halfWidth) < threshold) {
                    shallow = middle;
                } else {
                    deep = middle;
                }
            }

            return deep;
        }

        /// <summary>
        ///     Finds the first curve position that can be a mouth, going from one to another.
        ///     A node moved along its network to be a mouth goes there.
        ///     The runs of one edge do not tell where that is.
        ///     They keep a mouth a portal edge away from the start of the edge.
        ///     The mouth moves out from where the whole width has three limits, as in a split.
        ///     It may reach the position searched from, where the node is.
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
                    var anchor = i == 0
                        ? t
                        : Mouth(ref terrain, bezier, halfWidth, deep, previous, t);

                    return MoveOut(
                        ref terrain,
                        bezier,
                        halfWidth,
                        limit,
                        anchor,
                        math.sign(from - to),
                        from);
                }

                previous = t;
            }

            return -1f;
        }

        /// <summary>
        ///     Checks whether a node at a curve position can be a mouth.
        ///     The whole width needs two limits of ground over it, the least the game tunnels from.
        ///     The head wall must be level too: its top follows the ground on each side.
        ///     The game caps the ground at three limits, so a slant above that does not show.
        ///     A node placed for a mouth keeps <see cref="Tolerance" /> in hand on both.
        ///     A node that exists already may take it as slack.
        /// </summary>
        /// <param name="terrain">Terrain heights to measure against.</param>
        /// <param name="bezier">The curve.</param>
        /// <param name="t">Curve position of the node.</param>
        /// <param name="halfWidth">Half the width of the network.</param>
        /// <param name="limit">Elevation limit of the network prefab.</param>
        /// <param name="slack">Zero to place a node, <see cref="Tolerance" /> to judge one.</param>
        /// <returns>True if a mouth there has the cover and a level head wall.</returns>
        public static bool IsMouth(
            ref TerrainHeightData terrain,
            Bezier4x3             bezier,
            float                 t,
            float                 halfWidth,
            float                 limit,
            float                 slack) {
            var sides    = -Elevation(ref terrain, bezier, t, halfWidth);
            var cover    = Cover(ref terrain, bezier, t, halfWidth);
            var wallStep = math.min(math.cmax(sides), limit * 3f) - math.cmin(sides);

            return cover >= limit * 2f + Tolerance - slack && wallStep <= Tolerance + slack;
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
        ///     Gets the elevation to store on a curve that a tool writes in place.
        ///     Under the ground it is the measured one, and within the limit there is none.
        ///     Above the limit the curve keeps what it had.
        ///     A bridge stays one, and a ground road does not become one.
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
