namespace NetworkTools.Systems.Tools.Connect {
    using Colossal.Mathematics;

    using Game.Tools;

    using NetworkTools.Systems.Tools.RoadShape;
    using NetworkTools.Systems.Tools.Utils;

    using Unity.Collections;
    using Unity.Mathematics;

    public struct LoopGenerator : IConnectionGenerator {
        public void InitializeConfig(ref ConnectJobConfig config) {
            config.LoopRadiusFactor = 0.5f;
            config.LoopArcSide     = LoopArcSide.Outer;
        }

        public void GenerateConnection(
            in  ConnectJobConfig      config,
            ref NativeList<EdgeConfig> curves) {
            if (!ComputeLoopAxis(
                    config.StartPosition, config.StartDirection,
                    config.EndPosition, config.EndDirection,
                    out var naturalCenter, out var axisDir, out var isCCW))
                return;

            // Convert factor to center position on the axis
            var maxCenter = ComputeMaxCenter(naturalCenter, axisDir, config.StartPosition, config.EndPosition);
            var center    = math.lerp(naturalCenter, maxCenter, config.LoopRadiusFactor);

            // Compute how far the arc start must shift along start direction
            float2 c2 = center.xz;
            float2 d0 = math.normalizesafe(config.StartDirection.xz);
            float2 d1 = math.normalizesafe(config.EndDirection.xz);

            float3 startDirXZ = math.normalizesafe(new float3(config.StartDirection.x, 0f, config.StartDirection.z));
            float3 endDirXZ   = math.normalizesafe(new float3(config.EndDirection.x, 0f, config.EndDirection.z));

            // Arc start: perpendicular foot from center onto the start line
            float startOffset = math.max(0f, math.dot(c2 - config.StartPosition.xz, d0));
            float3 arcStart   = config.StartPosition + startDirXZ * startOffset;

            // Radius from the start tangent point (guaranteed on the circle)
            float radius = math.distance(c2, arcStart.xz);
            if (radius < 0.1f) return;

            // Arc end: find where this circle intersects the end line so both
            // endpoints are at exactly `radius` from center (no overshoot).
            //   Line: P(λ) = endPos + λ·d1,  |P - center|² = r²
            //   λ² + 2(v·d1)λ + (|v|² - r²) = 0   where v = endPos - center
            float  endOffset;
            float3 arcEnd;
            {
                float2 v    = config.EndPosition.xz - c2;
                float  vd   = math.dot(v, d1);
                float  disc = vd * vd - (math.dot(v, v) - radius * radius);

                if (disc >= 0f) {
                    float sqrtDisc = math.sqrt(disc);
                    float l1       = -vd + sqrtDisc;
                    float l2       = -vd - sqrtDisc;
                    // Pick the root closest to the naïve perpendicular projection
                    float naive = math.max(0f, math.dot(c2 - config.EndPosition.xz, d1));
                    endOffset = math.abs(l1 - naive) < math.abs(l2 - naive) ? l1 : l2;
                    endOffset = math.max(0f, endOffset);
                } else {
                    // Fallback (shouldn't happen with valid geometry)
                    endOffset = math.max(0f, math.dot(c2 - config.EndPosition.xz, d1));
                }
                arcEnd = config.EndPosition + endDirXZ * endOffset;
            }

            // Angles and sweep
            float startAngle = math.atan2(arcStart.z - center.z, arcStart.x - center.x);
            float endAngle   = math.atan2(arcEnd.z   - center.z, arcEnd.x   - center.x);

            // Compute sweep in the natural winding direction
            float sweep = endAngle - startAngle;
            if (isCCW) {
                if (sweep <= 0f) sweep += 2f * math.PI;
            } else {
                if (sweep >= 0f) sweep -= 2f * math.PI;
            }

            // Toggle between inner (shorter) and outer (longer) arc
            bool naturallyOuter = math.abs(sweep) > math.PI;
            bool wantOuter      = config.LoopArcSide == LoopArcSide.Outer;
            if (wantOuter != naturallyOuter)
                sweep += (sweep > 0f) ? -2f * math.PI : 2f * math.PI;

            // Subdivide arc into bezier segments (each <= 90 degrees)
            int   arcSegments = math.max(1, (int)math.ceil(math.abs(sweep) / (math.PI / 2f)));
            float segSweep    = sweep / arcSegments;
            float k           = (4f / 3f) * math.tan(segSweep / 4f);

            bool hasStartStraight = startOffset > 0.5f;
            bool hasEndStraight   = endOffset   > 0.5f;

            // ── Distribute height proportionally across total path length ──
            float startStraightLen = hasStartStraight ? startOffset : 0f;
            float arcLen           = radius * math.abs(sweep);
            float endStraightLen   = hasEndStraight ? endOffset : 0f;
            float totalLen         = startStraightLen + arcLen + endStraightLen;

            float startY = config.StartPosition.y;
            float endY   = config.EndPosition.y;

            float arcStartY, arcEndY;
            if (totalLen > 0.01f) {
                arcStartY = math.lerp(startY, endY, startStraightLen / totalLen);
                arcEndY   = math.lerp(startY, endY, (startStraightLen + arcLen) / totalLen);
            } else {
                arcStartY = startY;
                arcEndY   = endY;
            }

            // ── Emit start offset segment ──
            if (hasStartStraight) {
                var straightEnd = new float3(arcStart.x, arcStartY, arcStart.z);
                EmitStraightEdge(in config, ref curves,
                    config.StartPosition, straightEnd,
                    isFirst: true, isLast: false,
                    config.StartElevation, 0f);
            }

            // ── Emit arc segments (chained for floating-point precision) ──
            float3 prevEnd = arcStart;
            for (int i = 0; i < arcSegments; i++) {
                float a0 = startAngle + i * segSweep;
                float a1 = a0 + segSweep;

                // Chain positions: first starts at arcStart, last ends at arcEnd,
                // intermediates chain from the previous segment's exact endpoint.
                float3 pa = (i == 0)               ? arcStart : prevEnd;
                float3 pd = (i == arcSegments - 1) ? arcEnd
                    : new float3(center.x + radius * math.cos(a1), 0f, center.z + radius * math.sin(a1));

                // CCW tangent direction at angle theta: (-sin, cos)
                var tanA = new float3(-math.sin(a0), 0f, math.cos(a0));
                var tanD = new float3(-math.sin(a1), 0f, math.cos(a1));

                var pb = pa + k * radius * tanA;
                var pc = pd - k * radius * tanD;

                // Height interpolated proportionally across total path length
                float t0 = (float)i / arcSegments;
                float t1 = (float)(i + 1) / arcSegments;
                pa.y = math.lerp(arcStartY, arcEndY, t0);
                pd.y = math.lerp(arcStartY, arcEndY, t1);
                pb.y = math.lerp(pa.y, pd.y, 1f / 3f);
                pc.y = math.lerp(pa.y, pd.y, 2f / 3f);

                bool isFirst = !hasStartStraight && i == 0;
                bool isLast  = !hasEndStraight && i == arcSegments - 1;

                var bezier = new Bezier4x3 { a = pa, b = pb, c = pc, d = pd };
                curves.Add(new EdgeConfig {
                    Bezier             = bezier,
                    Length             = MathUtils.Length(bezier),
                    StartNodeElevation = isFirst ? SlopeUtils.ClampElevation(config.StartElevation) : 0f,
                    EndNodeElevation   = isLast  ? SlopeUtils.ClampElevation(config.EndElevation)   : 0f,
                    StartNodeFlags     = (isFirst ? CoursePosFlags.IsFirst : CoursePosFlags.DisableMerge) | CoursePosFlags.IsRight,
                    EndNodeFlags       = (isLast  ? CoursePosFlags.IsLast  : CoursePosFlags.DisableMerge) | CoursePosFlags.IsRight,
                    NetPrefabEntity     = config.NetPrefabEntity,
                    NetLanePrefabEntity = config.NetLanePrefabEntity,
                });

                prevEnd = pd;
            }

            // ── Emit end offset segment ──
            if (hasEndStraight) {
                var straightStart = new float3(arcEnd.x, arcEndY, arcEnd.z);
                EmitStraightEdge(in config, ref curves,
                    straightStart, config.EndPosition,
                    isFirst: false, isLast: true,
                    0f, config.EndElevation);
            }
        }

        // ── Straight edge helper ────────────────────────────────────────────────

        private static void EmitStraightEdge(
            in  ConnectJobConfig      config,
            ref NativeList<EdgeConfig> curves,
            float3 from, float3 to,
            bool isFirst, bool isLast,
            float startElev, float endElev) {
            var dir    = to - from;
            var bezier = new Bezier4x3 {
                a = from,
                b = from + dir * (1f / 3f),
                c = from + dir * (2f / 3f),
                d = to
            };
            curves.Add(new EdgeConfig {
                Bezier             = bezier,
                Length             = MathUtils.Length(bezier),
                StartNodeElevation = SlopeUtils.ClampElevation(startElev),
                EndNodeElevation   = SlopeUtils.ClampElevation(endElev),
                StartNodeFlags     = (isFirst ? CoursePosFlags.IsFirst : CoursePosFlags.DisableMerge) | CoursePosFlags.IsRight,
                EndNodeFlags       = (isLast  ? CoursePosFlags.IsLast  : CoursePosFlags.DisableMerge) | CoursePosFlags.IsRight,
                NetPrefabEntity     = config.NetPrefabEntity,
                NetLanePrefabEntity = config.NetLanePrefabEntity,
            });
        }

        // ── Static geometry helpers (used by both the generator and AxisHandle) ─

        /// <summary>
        ///     Computes the natural circle center (perpendicular-ray intersection) and the
        ///     drag-axis direction.  The axis is the direction the center moves when both
        ///     start/end offsets increase by the same amount — derived by differentiating
        ///     the ray intersection w.r.t. a shared offset δ applied to both origins.
        ///     Returns false if degenerate (parallel directions).
        /// </summary>
        public static bool ComputeLoopAxis(
            float3 startPos, float3 startDir,
            float3 endPos,   float3 endDir,
            out float3 naturalCenter, out float3 axisDir3D, out bool isCCW) {
            float2 p0 = startPos.xz;
            float2 p1 = endPos.xz;
            float2 d0 = math.normalizesafe(startDir.xz);
            float2 d1 = math.normalizesafe(endDir.xz);

            // Perpendicular to each direction (left of start, right of end)
            float2 perp0 = new float2(-d0.y, d0.x);
            float2 perp1 = new float2(d1.y, -d1.x);

            float2 dp    = p1 - p0;
            float  cross = perp0.x * perp1.y - perp0.y * perp1.x;
            float  y     = (startPos.y + endPos.y) * 0.5f;

            if (math.abs(cross) < 0.001f) {
                naturalCenter = (startPos + endPos) * 0.5f;
                axisDir3D     = math.normalizesafe(new float3(d0.x + d1.x, 0f, d0.y + d1.y));
                isCCW         = true;
                return false;
            }

            float  t  = (dp.x * perp1.y - dp.y * perp1.x) / cross;
            float2 nc = p0 + t * perp0;
            naturalCenter = new float3(nc.x, y, nc.y);
            isCCW         = t > 0f;

            // Axis direction: d(center)/dδ when both ray origins shift by δ along
            // their respective directions.  The shifted intersection satisfies
            //   t'·perp0 - s'·perp1 = dp + δ·(d1 - d0)
            // so dt/dδ = ((d1-d0)·perp1_y_col - (d1-d0)·perp1_x_col) / cross
            float2 dDiff   = d1 - d0;
            float  dtDelta = (dDiff.x * perp1.y - dDiff.y * perp1.x) / cross;

            // center(δ) = p0 + δ·d0 + (t + δ·dtDelta)·perp0
            // ⇒  d(center)/dδ = d0 + dtDelta·perp0
            float2 axisDir = math.normalizesafe(d0 + dtDelta * perp0);

            axisDir3D = new float3(axisDir.x, 0f, axisDir.y);
            return true;
        }

        /// <summary>
        ///     Returns the far end of the drag axis, one chord-length out from the
        ///     natural center along the axis direction.
        /// </summary>
        public static float3 ComputeMaxCenter(float3 naturalCenter, float3 axisDir, float3 startPos, float3 endPos) {
            float chordLength = math.distance(startPos.xz, endPos.xz);
            return naturalCenter + axisDir * math.max(chordLength, 50f);
        }
    }
}
