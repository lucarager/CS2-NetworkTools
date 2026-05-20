namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Collections;
    using Colossal.Mathematics;

    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;
    using Game.Zones;
    using NetworkTools.Systems.Tools.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    ///     Job definitions for <see cref="NT_GenerateToolSystem" />.
    /// </summary>
    public partial class NT_GenerateToolSystem {
#if BURST
        [BurstCompile]
#endif
        internal struct CreateDefinitionsJob : IJob {
            [ReadOnly] public required GenerateMode      Mode;
            [ReadOnly] public required GenerateJobConfig Config;
            [ReadOnly] public required ToolOutputMode    OutputMode;
            [ReadOnly] public required Entity            NetPrefabEntity;
            [ReadOnly] public required Entity            NetLanePrefabEntity;

            [ReadOnly] public required ComponentLookup<Node> NodeLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef> PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge> ConnectedEdgeLookup;
            [ReadOnly] public required ComponentLookup<Edge> EdgeLookup;
            [ReadOnly] public required ComponentLookup<Curve> CurveLookup;
            [ReadOnly] public required ComponentLookup<Upgraded> UpgradedLookup;
            [ReadOnly] public required ComponentLookup<Aggregated> AggregatedLookup;
            [ReadOnly] public required ComponentLookup<NetGeometryData> NetGeometryDataLookup;

            public required EntityCommandBuffer ECB;

            public void Execute() {
                // 1. Create data structures
                var curves = new NativeList<EdgeConfig>(64, Allocator.Temp);

                // Resolve all runtime prefab data into a local config copy so generators
                // receive a self-contained snapshot without needing component lookups.
                var config = Config;
                config.NetPrefabEntity    = NetPrefabEntity;
                config.NetLanePrefabEntity = NetLanePrefabEntity;
                if (NetGeometryDataLookup.TryGetComponent(NetPrefabEntity, out var netGeom)) {
                    config.NetWidth       = netGeom.m_DefaultWidth;
                    config.ElevationLimit = netGeom.m_ElevationLimit;
                }
                config.AltNetPrefabXWidth = NetGeometryDataLookup.TryGetComponent(config.AltNetPrefabXEntity, out var altXGeom)
                    ? altXGeom.m_DefaultWidth : config.NetWidth;
                config.AltNetPrefabZWidth = NetGeometryDataLookup.TryGetComponent(config.AltNetPrefabZEntity, out var altZGeom)
                    ? altZGeom.m_DefaultWidth : config.NetWidth;

                // 2. Create definitions
                switch (Mode)
                {
                    case GenerateMode.Grid:
                        new GridGenerator().Generate(config, ref curves);
                        break;
                    case GenerateMode.Circle:
                        new CircleGenerator().Generate(config, ref curves);
                        break;
                    case GenerateMode.Oval:
                        new OvalGenerator().Generate(config, ref curves);
                        break;
                }

                // 3. Output
                Output(curves);

                // Cleanup
                curves.Dispose();
            }

            private void Output(NativeList<EdgeConfig> curves) {
                for (var i = 0; i < curves.Length; i++)
                {
                    var curve = curves[i];
                    OutputPreviewEdge(curve);
                }
            }

            private void OutputPreviewEdge(EdgeConfig EC) {
                var definitionEntity = ECB.CreateEntity();

                var creationDefinition = new CreationDefinition {
                    m_Original = Entity.Null,
                    m_Prefab = EC.NetPrefabEntity,
                    m_SubPrefab = EC.NetLanePrefabEntity,
                    m_Flags = CreationFlags.SubElevation,
                };

                ECB.AddComponent(definitionEntity, creationDefinition);
                ECB.AddComponent<Updated>(definitionEntity);

                var startElevation = new float2(EC.StartNodeElevation);
                var endElevation = new float2(EC.EndNodeElevation);

                var netCourse = new NetCourse {
                    m_Curve = EC.Bezier,
                    m_Length = EC.Length,
                    m_FixedIndex = -1,
                    m_StartPosition = new CoursePos {
                        m_Entity = EC.StartNodeEntity,
                        m_Position = EC.Bezier.a,
                        m_Rotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(EC.Bezier)),
                        m_CourseDelta = 0,
                        m_Elevation = startElevation,
                        m_Flags = EC.StartNodeFlags,
                        m_ParentMesh = -1,
                        m_SplitPosition = 0
                    },
                    m_EndPosition = new CoursePos {
                        m_Entity = EC.EndNodeEntity,
                        m_Position = EC.Bezier.d,
                        m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(EC.Bezier)),
                        m_CourseDelta = 1,
                        m_Elevation = endElevation,
                        m_Flags = EC.EndNodeFlags,
                        m_ParentMesh = -1,
                        m_SplitPosition = 0
                    }
                };

                ECB.AddComponent(definitionEntity, netCourse);
            }
        }

        /// <summary>
        ///     Snaps a raw raycast control point to nearby geometry, objects, zone grids, and
        ///     guide lines. Modeled after the base game's <c>NetToolSystem.SnapJob</c> but
        ///     simplified for the generate tool's single-point placement (no multi-control-point
        ///     chains, no replace mode, no shoreline snap).
        ///     <para>
        ///     Each snap type is gated by a <see cref="SnapOption"/> flag so the player can
        ///     toggle them independently from the UI.  The job runs on a single thread before
        ///     <see cref="CreateDefinitionsJob"/> and writes the winning snap candidate into
        ///     <see cref="m_SnappedControlPoint"/>.
        ///     </para>
        /// </summary>
#if BURST
        [BurstCompile]
#endif
        internal struct SnapPlacementJob : IJob {
            // ── Inputs ───────────────────────────────────────────────────────────

            /// <summary>Raw raycast hit point from <see cref="NT_GenerateToolSystem.GetRaycastResult"/>.</summary>
            [ReadOnly] public ControlPoint m_ControlPoint;

            /// <summary>Bitfield of active snap options chosen by the player.</summary>
            [ReadOnly] public SnapOption   m_SnapFlags;

            /// <summary>The net prefab entity being placed (determines width, layers, height range).</summary>
            [ReadOnly] public Entity       m_Prefab;

            /// <summary>Current elevation offset from the generate tool's Elevation parameter.</summary>
            [ReadOnly] public float        m_Elevation;

            // ── Search trees (spatial indices for nearby entities) ────────────────

            [ReadOnly] public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetSearchTree;
            [ReadOnly] public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_ObjectSearchTree;
            [ReadOnly] public NativeQuadTree<Entity, Bounds2>          m_ZoneSearchTree;

            // ── Terrain / water (for height-based snap validation) ────────────────

            [ReadOnly] public TerrainHeightData             m_TerrainHeightData;
            [ReadOnly] public WaterSurfaceData<SurfaceWater> m_WaterSurfaceData;

            // ── Component lookups (ECS random-access reads) ──────────────────────

            [ReadOnly] public ComponentLookup<Game.Net.Node>  m_NodeData;
            [ReadOnly] public ComponentLookup<Edge>            m_EdgeData;
            [ReadOnly] public ComponentLookup<Curve>           m_CurveData;
            [ReadOnly] public ComponentLookup<Owner>           m_OwnerData;
            [ReadOnly] public ComponentLookup<Road>            m_RoadData;
            [ReadOnly] public ComponentLookup<Composition>     m_CompositionData;
            [ReadOnly] public ComponentLookup<EdgeGeometry>    m_EdgeGeometryData;
            [ReadOnly] public ComponentLookup<PrefabRef>       m_PrefabRefData;
            [ReadOnly] public ComponentLookup<NetData>         m_PrefabNetData;
            [ReadOnly] public ComponentLookup<NetGeometryData> m_PrefabGeometryData;
            [ReadOnly] public ComponentLookup<PlaceableNetData> m_PlaceableData;
            [ReadOnly] public ComponentLookup<NetCompositionData> m_PrefabCompositionData;
            [ReadOnly] public ComponentLookup<RoadComposition> m_RoadCompositionData;
            [ReadOnly] public ComponentLookup<Game.Objects.Transform> m_TransformData;
            [ReadOnly] public ComponentLookup<BuildingData>    m_BuildingData;
            [ReadOnly] public ComponentLookup<ObjectGeometryData> m_ObjectGeometryData;
            [ReadOnly] public ComponentLookup<Block>           m_ZoneBlockData;
            [ReadOnly] public ComponentLookup<LocalConnectData> m_LocalConnectData;

            // ── Buffer lookups ───────────────────────────────────────────────────

            [ReadOnly] public BufferLookup<ConnectedEdge> m_ConnectedEdges;
            [ReadOnly] public BufferLookup<Cell>          m_ZoneCells;
            [ReadOnly] public BufferLookup<Game.Net.SubNet> m_SubNets;
            [ReadOnly] public BufferLookup<NetCompositionArea> m_PrefabCompositionAreas;

            // ── Outputs ──────────────────────────────────────────────────────────

            /// <summary>Best snap candidate. If no snap wins, this holds the raw hit position.</summary>
            public NativeValue<ControlPoint> m_SnappedControlPoint;

            /// <summary>Entity of the snap target, or <see cref="Entity.Null"/> if no snap.</summary>
            public NativeValue<Entity>       m_SnappedEntity;

            /// <summary>Accumulated snap lines for visual feedback rendering.</summary>
            public NativeList<SnapLine>      m_SnapLines;

            /// <summary>
            ///     Main entry point. Resolves prefab data, then runs each enabled snap handler
            ///     in priority order: geometry first (strongest snaps), then objects, then zone
            ///     grid. Each handler competes via <see cref="ToolUtils.AddSnapPosition"/>; the
            ///     highest-priority candidate wins.
            /// </summary>
            public void Execute() {
                // Resolve prefab data for the net being placed — these drive snap radius,
                // layer compatibility, and height-range checks inside the iterators.
                NetGeometryData netGeometryData = default;
                PlaceableNetData placeableNetData = default;
                NetData netData = m_PrefabNetData[m_Prefab];
                RoadData roadData = default;

                if (m_PrefabGeometryData.HasComponent(m_Prefab))
                    netGeometryData = m_PrefabGeometryData[m_Prefab];
                if (m_PlaceableData.HasComponent(m_Prefab))
                    placeableNetData = m_PlaceableData[m_Prefab];

                // Guarantee a minimum snap distance so very narrow nets can still snap
                placeableNetData.m_SnapDistance = math.max(placeableNetData.m_SnapDistance, 1f);

                m_SnapLines.Clear();

                // Start with the raw hit position as the fallback — iterators will
                // replace this via AddSnapPosition if they find a better candidate.
                var bestSnapPosition = m_ControlPoint;
                bestSnapPosition.m_Position = bestSnapPosition.m_HitPosition;
                bestSnapPosition.m_OriginalEntity = Entity.Null;

                // Run handlers in the same order as the base game's SnapJob.Execute():
                // existing geometry (nodes/edges + guide lines), then objects, then zone grid.
                if ((m_SnapFlags & (SnapOption.ExistingGeometry | SnapOption.GuideLines)) != SnapOption.None)
                    HandleExistingGeometry(ref bestSnapPosition, netData, roadData, netGeometryData, placeableNetData);

                if ((m_SnapFlags & SnapOption.ObjectSide) != SnapOption.None)
                    HandleExistingObjects(ref bestSnapPosition, netData, netGeometryData, placeableNetData);

                if ((m_SnapFlags & SnapOption.ZoneGrid) != SnapOption.None)
                    HandleZoneGrid(ref bestSnapPosition, netGeometryData);

                m_SnappedControlPoint.value = bestSnapPosition;
                m_SnappedEntity.value = bestSnapPosition.m_OriginalEntity;
            }

            /// <summary>
            ///     Sets up and runs the <see cref="NetIterator"/> over the net search tree.
            ///     Uses two bounding volumes: a tight one for geometry snaps (based on net width)
            ///     and a larger one that includes the guide line search radius.
            /// </summary>
            private void HandleExistingGeometry(ref ControlPoint bestSnapPosition, NetData netData, RoadData roadData, NetGeometryData netGeometryData, PlaceableNetData placeableNetData) {
                // When only GuideLines is active (ExistingGeometry off), zero the snap offset
                // so geometry snapping is effectively disabled but guide lines still emit.
                var snapOffset = placeableNetData.m_SnapDistance;
                if ((m_SnapFlags & SnapOption.ExistingGeometry) == SnapOption.None)
                    snapOffset = 0f;
                var searchRadius = netGeometryData.m_DefaultWidth + snapOffset;
                var guideLength = placeableNetData.m_SnapDistance * 64f;

                LocalConnectData localConnectData = default;
                if (m_LocalConnectData.HasComponent(m_Prefab))
                    localConnectData = m_LocalConnectData[m_Prefab];

                var heightRange = new Bounds1(-50f, 50f) | localConnectData.m_HeightRange;

                var bounds = new Bounds3 {
                    xz = new Bounds2(m_ControlPoint.m_HitPosition.xz - searchRadius, m_ControlPoint.m_HitPosition.xz + searchRadius),
                    y = m_ControlPoint.m_HitPosition.y + heightRange
                };
                var totalBounds = bounds;
                if ((m_SnapFlags & SnapOption.GuideLines) != SnapOption.None) {
                    totalBounds.min -= guideLength;
                    totalBounds.max += guideLength;
                }

                var netIterator = new NetIterator {
                    m_TotalBounds = totalBounds,
                    m_Bounds = bounds,
                    m_SnapFlags = m_SnapFlags,
                    m_SnapOffset = snapOffset,
                    m_SnapDistance = placeableNetData.m_SnapDistance,
                    m_Elevation = m_Elevation,
                    m_GuideLength = guideLength,
                    m_HeightRange = heightRange,
                    m_NetData = netData,
                    m_PrefabRoadData = roadData,
                    m_NetGeometryData = netGeometryData,
                    m_LocalConnectData = localConnectData,
                    m_ControlPoint = m_ControlPoint,
                    m_BestSnapPosition = bestSnapPosition,
                    m_SnapLines = m_SnapLines,
                    m_TerrainHeightData = m_TerrainHeightData,
                    m_WaterSurfaceData = m_WaterSurfaceData,
                    m_OwnerData = m_OwnerData,
                    m_NodeData = m_NodeData,
                    m_EdgeData = m_EdgeData,
                    m_CurveData = m_CurveData,
                    m_CompositionData = m_CompositionData,
                    m_EdgeGeometryData = m_EdgeGeometryData,
                    m_RoadData = m_RoadData,
                    m_PrefabRefData = m_PrefabRefData,
                    m_PrefabNetData = m_PrefabNetData,
                    m_PrefabGeometryData = m_PrefabGeometryData,
                    m_PrefabCompositionData = m_PrefabCompositionData,
                    m_RoadCompositionData = m_RoadCompositionData,
                    m_ConnectedEdges = m_ConnectedEdges,
                    m_SubNets = m_SubNets,
                    m_PrefabCompositionAreas = m_PrefabCompositionAreas,
                };

                // First try direct snap to the entity under the cursor (the raycast hit).
                // If the raycast landed on a net entity, try geometry snap; if that fails,
                // fall back to guide lines from that entity.
                if ((m_SnapFlags & SnapOption.ExistingGeometry) != SnapOption.None && m_PrefabRefData.HasComponent(m_ControlPoint.m_OriginalEntity)) {
                    var prefabRef = m_PrefabRefData[m_ControlPoint.m_OriginalEntity];
                    if (!netIterator.HandleGeometry(m_ControlPoint, m_ControlPoint.m_HitPosition.y, prefabRef, true)
                        && (m_SnapFlags & SnapOption.GuideLines) != SnapOption.None) {
                        netIterator.HandleGuideLines(m_ControlPoint.m_OriginalEntity);
                    }
                }

                // Then iterate the spatial tree for all nearby net entities
                m_NetSearchTree.Iterate(ref netIterator, 0);
                bestSnapPosition = netIterator.m_BestSnapPosition;
            }

            /// <summary>
            ///     Sets up and runs the <see cref="ObjectIterator"/> over the static object
            ///     search tree. Handles snapping to building lot edges and to standalone net
            ///     nodes owned by objects (e.g. a bus stop node belonging to a building).
            /// </summary>
            private void HandleExistingObjects(ref ControlPoint bestSnapPosition, NetData netData, NetGeometryData netGeometryData, PlaceableNetData placeableNetData) {
                var snapOffset = netGeometryData.m_DefaultWidth * 0.5f + placeableNetData.m_SnapDistance;
                var bounds = new Bounds3(
                    m_ControlPoint.m_HitPosition - snapOffset,
                    m_ControlPoint.m_HitPosition + snapOffset
                );

                var objectIterator = new ObjectIterator {
                    m_Bounds = bounds,
                    m_SnapFlags = m_SnapFlags,
                    m_MaxDistance = snapOffset,
                    m_NetSnapOffset = placeableNetData.m_SnapDistance,
                    m_ObjectSnapOffset = placeableNetData.m_SnapDistance,
                    m_NetData = netData,
                    m_NetGeometryData = netGeometryData,
                    m_ControlPoint = m_ControlPoint,
                    m_BestSnapPosition = bestSnapPosition,
                    m_SnapLines = m_SnapLines,
                    m_OwnerData = m_OwnerData,
                    m_CurveData = m_CurveData,
                    m_NodeData = m_NodeData,
                    m_TransformData = m_TransformData,
                    m_PrefabRefData = m_PrefabRefData,
                    m_BuildingData = m_BuildingData,
                    m_ObjectGeometryData = m_ObjectGeometryData,
                    m_PrefabNetData = m_PrefabNetData,
                    m_PrefabGeometryData = m_PrefabGeometryData,
                    m_ConnectedEdges = m_ConnectedEdges,
                };

                m_ObjectSearchTree.Iterate(ref objectIterator, 0);
                bestSnapPosition = objectIterator.m_BestSnapPosition;
            }

            /// <summary>
            ///     Sets up and runs the <see cref="ZoneIterator"/> to snap to visible zone
            ///     cell boundaries. Once the closest visible cell is found, the hit position
            ///     is projected onto the zone grid (8m cell spacing) so the placement aligns
            ///     with the zone grid's axes.
            /// </summary>
            private void HandleZoneGrid(ref ControlPoint bestSnapPosition, NetGeometryData netGeometryData) {
                int cellWidth = ZoneUtils.GetCellWidth(netGeometryData.m_DefaultWidth);
                // Search radius: (cellWidth + 1) cells * 4m per half-cell * sqrt(2) for diagonal
                float searchRadius = (cellWidth + 1) * 4f * 1.4142135f;
                // Even-width nets need a half-cell offset to center on cell boundaries
                float cellOffset = math.select(0f, 4f, (cellWidth & 1) == 0);

                var zoneIterator = new ZoneIterator {
                    m_Bounds = new Bounds2(m_ControlPoint.m_HitPosition.xz - searchRadius, m_ControlPoint.m_HitPosition.xz + searchRadius),
                    m_HitPosition = m_ControlPoint.m_HitPosition.xz,
                    m_BestDistance = searchRadius,
                    m_ZoneBlockData = m_ZoneBlockData,
                    m_ZoneCells = m_ZoneCells,
                };

                m_ZoneSearchTree.Iterate(ref zoneIterator, 0);

                if (zoneIterator.m_BestDistance < searchRadius) {
                    // Project the hit offset onto the zone grid's local axes and snap to
                    // 8m intervals (the game's zone cell size).
                    var offset = m_ControlPoint.m_HitPosition.xz - zoneIterator.m_BestPosition.xz;
                    var right = MathUtils.Right(zoneIterator.m_BestDirection);
                    var snapForward = MathUtils.Snap(math.dot(offset, zoneIterator.m_BestDirection), 8f, cellOffset);
                    var snapRight = MathUtils.Snap(math.dot(offset, right), 8f, cellOffset);

                    var candidate = m_ControlPoint;
                    // Only preserve the original entity if it's a net entity (not terrain)
                    if (!m_EdgeData.HasComponent(m_ControlPoint.m_OriginalEntity) && !m_NodeData.HasComponent(m_ControlPoint.m_OriginalEntity))
                        candidate.m_OriginalEntity = Entity.Null;

                    candidate.m_Direction = zoneIterator.m_BestDirection;
                    candidate.m_Position.xz = zoneIterator.m_BestPosition.xz + zoneIterator.m_BestDirection * snapForward + right * snapRight;
                    candidate.m_SnapPriority = ToolUtils.CalculateSnapPriority(0f, 1f, 1f, m_ControlPoint.m_HitPosition, candidate.m_Position, candidate.m_Direction);

                    // Emit hidden cross-hair snap lines along both grid axes for visual feedback
                    var line = new Line3(candidate.m_Position, candidate.m_Position);
                    var line2 = new Line3(candidate.m_Position, candidate.m_Position);
                    line.a.xz -= candidate.m_Direction * 8f;
                    line.b.xz += candidate.m_Direction * 8f;
                    line2.a.xz -= MathUtils.Right(candidate.m_Direction) * 8f;
                    line2.b.xz += MathUtils.Right(candidate.m_Direction) * 8f;

                    ToolUtils.AddSnapPosition(ref bestSnapPosition, candidate);
                    ToolUtils.AddSnapLine(ref bestSnapPosition, m_SnapLines, new SnapLine(candidate, NetUtils.StraightCurve(line.a, line.b), SnapLineFlags.Hidden, 1f));
                    candidate.m_Direction = MathUtils.Right(candidate.m_Direction);
                    ToolUtils.AddSnapLine(ref bestSnapPosition, m_SnapLines, new SnapLine(candidate, NetUtils.StraightCurve(line2.a, line2.b), SnapLineFlags.Hidden, 1f));
                }
            }

            // ═══════════════════════════════════════════════════════════════════════
            // NetIterator — snaps to existing nodes and edges
            // ═══════════════════════════════════════════════════════════════════════

            /// <summary>
            ///     Quad-tree iterator that visits net entities (nodes and edges) and produces
            ///     snap candidates.  Handles three snap behaviors:
            ///     <list type="bullet">
            ///         <item><b>Node snap</b> — snaps to standalone nodes (dead-ends, orphans)</item>
            ///         <item><b>Edge snap</b> — snaps to the closest point on a curve (via <see cref="HandleCurve"/>),
            ///             including segment-area snapping for nets with buildable net areas</item>
            ///         <item><b>Guide lines</b> — emits directional guide lines extending from
            ///             edge endpoints, with perpendicular lines at dead-end nodes</item>
            ///     </list>
            ///     Uses two bounding volumes: <see cref="m_TotalBounds"/> (includes guide line
            ///     radius) for the outer Intersect check, and <see cref="m_Bounds"/> (tight,
            ///     geometry-only) to gate <see cref="HandleGeometry(Entity)"/>.
            /// </summary>
            private struct NetIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
                /// <summary>Outer bounds including guide line search radius.</summary>
                public Bounds3 m_TotalBounds;
                /// <summary>Tight bounds for geometry-only snapping (net width + snap offset).</summary>
                public Bounds3 m_Bounds;
                public SnapOption m_SnapFlags;
                public float m_SnapOffset;
                public float m_SnapDistance;
                public float m_Elevation;
                public float m_GuideLength;
                public Bounds1 m_HeightRange;
                public NetData m_NetData;
                public RoadData m_PrefabRoadData;
                public NetGeometryData m_NetGeometryData;
                public LocalConnectData m_LocalConnectData;
                public ControlPoint m_ControlPoint;
                public ControlPoint m_BestSnapPosition;
                public NativeList<SnapLine> m_SnapLines;
                public TerrainHeightData m_TerrainHeightData;
                public WaterSurfaceData<SurfaceWater> m_WaterSurfaceData;
                public ComponentLookup<Owner> m_OwnerData;
                public ComponentLookup<Game.Net.Node> m_NodeData;
                public ComponentLookup<Edge> m_EdgeData;
                public ComponentLookup<Curve> m_CurveData;
                public ComponentLookup<Composition> m_CompositionData;
                public ComponentLookup<EdgeGeometry> m_EdgeGeometryData;
                public ComponentLookup<Road> m_RoadData;
                public ComponentLookup<PrefabRef> m_PrefabRefData;
                public ComponentLookup<NetData> m_PrefabNetData;
                public ComponentLookup<NetGeometryData> m_PrefabGeometryData;
                public ComponentLookup<NetCompositionData> m_PrefabCompositionData;
                public ComponentLookup<RoadComposition> m_RoadCompositionData;
                public BufferLookup<ConnectedEdge> m_ConnectedEdges;
                public BufferLookup<Game.Net.SubNet> m_SubNets;
                public BufferLookup<NetCompositionArea> m_PrefabCompositionAreas;

                public bool Intersect(QuadTreeBoundsXZ bounds) {
                    return MathUtils.Intersect(bounds.m_Bounds, m_TotalBounds);
                }

                /// <summary>
                ///     Called for each entity whose AABB overlaps <see cref="m_TotalBounds"/>.
                ///     First attempts geometry snap (only within tight bounds), then falls
                ///     through to guide lines if geometry snap didn't claim the entity.
                /// </summary>
                public void Iterate(QuadTreeBoundsXZ bounds, Entity entity) {
                    if (!MathUtils.Intersect(bounds.m_Bounds, m_TotalBounds))
                        return;
                    // Skip the entity the raycast already hit — it's handled separately
                    // in HandleExistingGeometry's direct-snap path above.
                    if (entity == m_ControlPoint.m_OriginalEntity && (m_SnapFlags & SnapOption.ExistingGeometry) != SnapOption.None)
                        return;

                    // Try geometry snap first; if it succeeds, skip guide lines for this entity
                    if (MathUtils.Intersect(bounds.m_Bounds, m_Bounds) && (m_SnapFlags & SnapOption.ExistingGeometry) != SnapOption.None && HandleGeometry(entity))
                        return;

                    if ((m_SnapFlags & SnapOption.GuideLines) != SnapOption.None)
                        HandleGuideLines(entity);
                }

                /// <summary>
                ///     Attempts to snap to a net entity (node or edge). Dispatches to node
                ///     or curve handling based on the entity's components.
                /// </summary>
                /// <returns>True if a snap candidate was produced.</returns>
                public bool HandleGeometry(Entity entity) {
                    if (!m_PrefabRefData.HasComponent(entity))
                        return false;

                    var prefabRef = m_PrefabRefData[entity];
                    var controlPoint = m_ControlPoint;
                    controlPoint.m_OriginalEntity = entity;

                    var snapRadius = m_NetGeometryData.m_DefaultWidth * 0.5f + m_SnapOffset;

                    if (m_ConnectedEdges.HasBuffer(entity) && m_NodeData.HasComponent(entity)) {
                        // Node snap
                        var node = m_NodeData[entity];
                        var connectedEdges = m_ConnectedEdges[entity];

                        // Skip nodes that are connected to edges (intersection nodes)
                        for (int i = 0; i < connectedEdges.Length; i++) {
                            var edge = m_EdgeData[connectedEdges[i].m_Edge];
                            if (edge.m_Start == entity || edge.m_End == entity)
                                return false;
                        }

                        if (m_PrefabGeometryData.HasComponent(prefabRef.m_Prefab)) {
                            var otherGeom = m_PrefabGeometryData[prefabRef.m_Prefab];
                            snapRadius += otherGeom.m_DefaultWidth * 0.5f;
                        }

                        if (math.distance(node.m_Position.xz, m_ControlPoint.m_HitPosition.xz) >= snapRadius)
                            return false;

                        return HandleGeometry(controlPoint, node.m_Position.y, prefabRef, false);
                    }

                    if (m_CurveData.HasComponent(entity)) {
                        // Edge snap
                        var curve = m_CurveData[entity];
                        if (m_CompositionData.HasComponent(entity)) {
                            var composition = m_CompositionData[entity];
                            var compositionData = m_PrefabCompositionData[composition.m_Edge];
                            snapRadius += compositionData.m_Width * 0.5f;
                        }

                        if (MathUtils.Distance(curve.m_Bezier.xz, m_ControlPoint.m_HitPosition.xz, out controlPoint.m_CurvePosition) >= snapRadius)
                            return false;

                        var curveHeight = MathUtils.Position(curve.m_Bezier, controlPoint.m_CurvePosition).y;
                        return HandleGeometry(controlPoint, curveHeight, prefabRef, false);
                    }

                    return false;
                }

                /// <summary>
                ///     Core geometry snap logic. Validates layer compatibility and height range,
                ///     then produces a snap candidate for either a standalone node or an edge curve.
                /// </summary>
                /// <param name="controlPoint">The control point with m_OriginalEntity set to the snap target.</param>
                /// <param name="snapHeight">World-space Y height of the snap target.</param>
                /// <param name="prefabRef">Prefab reference of the snap target entity.</param>
                /// <param name="ignoreHeightDistance">True when snapping to the raycast-hit entity (skip height range check).</param>
                /// <returns>True if a snap candidate was produced.</returns>
                public bool HandleGeometry(ControlPoint controlPoint, float snapHeight, PrefabRef prefabRef, bool ignoreHeightDistance) {
                    if (!m_PrefabNetData.HasComponent(prefabRef.m_Prefab))
                        return false;

                    var netData = m_PrefabNetData[prefabRef.m_Prefab];
                    bool snapAdded = false;

                    // Height check
                    float baseHeight;
                    if (m_Elevation < 0f)
                        baseHeight = TerrainUtils.SampleHeight(ref m_TerrainHeightData, controlPoint.m_HitPosition) + m_Elevation;
                    else
                        baseHeight = WaterUtils.SampleHeight(ref m_WaterSurfaceData, ref m_TerrainHeightData, controlPoint.m_HitPosition) + m_Elevation;

                    if (m_PrefabGeometryData.HasComponent(prefabRef.m_Prefab)) {
                        var otherGeom = m_PrefabGeometryData[prefabRef.m_Prefab];
                        var myRange = m_NetGeometryData.m_DefaultHeightRange + baseHeight;
                        var otherRange = otherGeom.m_DefaultHeightRange + snapHeight;
                        if (!MathUtils.Intersect(myRange, otherRange))
                            return false;
                    }

                    if (!NetUtils.CanConnect(netData, m_NetData))
                        return false;

                    var heightDist = snapHeight - baseHeight;
                    if (!ignoreHeightDistance && !MathUtils.Intersect(m_HeightRange, heightDist))
                        return false;

                    if (m_NodeData.HasComponent(controlPoint.m_OriginalEntity)) {
                        if (m_ConnectedEdges.HasBuffer(controlPoint.m_OriginalEntity)) {
                            var connectedEdges = m_ConnectedEdges[controlPoint.m_OriginalEntity];
                            if (connectedEdges.Length != 0) {
                                for (int i = 0; i < connectedEdges.Length; i++) {
                                    var edge = m_EdgeData[connectedEdges[i].m_Edge];
                                    if (edge.m_Start == controlPoint.m_OriginalEntity || edge.m_End == controlPoint.m_OriginalEntity)
                                        HandleCurve(controlPoint, connectedEdges[i].m_Edge, true, ref snapAdded);
                                }
                                return snapAdded;
                            }
                        }

                        // Standalone node (no connected edges or empty buffer)
                        var candidate = controlPoint;
                        var node = m_NodeData[controlPoint.m_OriginalEntity];
                        candidate.m_Position = node.m_Position;
                        candidate.m_Direction = math.mul(node.m_Rotation, new float3(0f, 0f, 1f)).xz;
                        MathUtils.TryNormalize(ref candidate.m_Direction);

                        float snapPrio = 1f;
                        if ((m_NetGeometryData.m_Flags & Game.Net.GeometryFlags.StrictNodes) != 0) {
                            var halfWidth = m_NetGeometryData.m_DefaultWidth * 0.5f;
                            if (m_PrefabGeometryData.HasComponent(prefabRef.m_Prefab))
                                halfWidth += m_PrefabGeometryData[prefabRef.m_Prefab].m_DefaultWidth * 0.5f;
                            if (math.distance(node.m_Position.xz, controlPoint.m_HitPosition.xz) <= halfWidth)
                                snapPrio = 2f;
                        }

                        candidate.m_SnapPriority = ToolUtils.CalculateSnapPriority(snapPrio, 1f, 1f, controlPoint.m_HitPosition, candidate.m_Position, candidate.m_Direction);
                        ToolUtils.AddSnapPosition(ref m_BestSnapPosition, candidate);
                        snapAdded = true;
                    } else if (m_CurveData.HasComponent(controlPoint.m_OriginalEntity)) {
                        HandleCurve(controlPoint, controlPoint.m_OriginalEntity, true, ref snapAdded);
                    }

                    return snapAdded;
                }

                /// <summary>
                ///     Snaps to a point on an edge's bezier curve. Handles composition width,
                ///     buildable net areas, endpoint snapping, and edge-split restrictions.
                ///     When <paramref name="allowEdgeSnap"/> is false (owned edges), only
                ///     endpoint snaps are permitted — mid-edge splits are rejected.
                /// </summary>
                private void HandleCurve(ControlPoint controlPoint, Entity curveEntity, bool allowEdgeSnap, ref bool snapAdded) {
                    if (!m_CurveData.HasComponent(curveEntity))
                        return;

                    var halfWidth = m_NetGeometryData.m_DefaultWidth * 0.5f;

                    PrefabRef prefabRef = m_PrefabRefData[curveEntity];
                    NetGeometryData otherGeom = default;
                    if (m_PrefabGeometryData.HasComponent(prefabRef.m_Prefab))
                        otherGeom = m_PrefabGeometryData[prefabRef.m_Prefab];

                    if (m_CompositionData.HasComponent(curveEntity)) {
                        var composition = m_CompositionData[curveEntity];
                        var compositionData = m_PrefabCompositionData[composition.m_Edge];
                        halfWidth += compositionData.m_Width * 0.5f;

                        if ((m_NetGeometryData.m_Flags & Game.Net.GeometryFlags.SnapToNetAreas) != 0) {
                            var areas = m_PrefabCompositionAreas[composition.m_Edge];
                            var edgeGeom = m_EdgeGeometryData[curveEntity];
                            if (SnapSegmentAreas(controlPoint, compositionData, areas, edgeGeom.m_Start, ref snapAdded)
                                | SnapSegmentAreas(controlPoint, compositionData, areas, edgeGeom.m_End, ref snapAdded))
                                return;
                        }
                    }

                    // Edge owner check
                    if (m_OwnerData.TryGetComponent(curveEntity, out var owner)
                        && !m_EdgeData.HasComponent(owner.m_Owner)) {
                        allowEdgeSnap = false;
                    }

                    var curve = m_CurveData[curveEntity];
                    float curvePos;
                    var dist = MathUtils.Distance(curve.m_Bezier.xz, controlPoint.m_HitPosition.xz, out curvePos);

                    var candidate = controlPoint;
                    curvePos = math.saturate(curvePos);

                    // Snap to endpoints
                    if (curvePos >= 0.5f) {
                        if (math.distance(curve.m_Bezier.d.xz, controlPoint.m_HitPosition.xz) < m_SnapOffset)
                            curvePos = 1f;
                    } else {
                        if (math.distance(curve.m_Bezier.a.xz, controlPoint.m_HitPosition.xz) < m_SnapOffset)
                            curvePos = 0f;
                    }
                    candidate.m_CurvePosition = curvePos;

                    if (!allowEdgeSnap && curvePos > 0f && curvePos < 1f) {
                        if (curvePos >= 0.5f) {
                            if (math.distance(curve.m_Bezier.d.xz, controlPoint.m_HitPosition.xz) < halfWidth + m_SnapOffset) {
                                curvePos = 1f;
                                candidate.m_CurvePosition = 1f;
                            } else return;
                        } else {
                            if (math.distance(curve.m_Bezier.a.xz, controlPoint.m_HitPosition.xz) < halfWidth + m_SnapOffset) {
                                curvePos = 0f;
                                candidate.m_CurvePosition = 0f;
                            } else return;
                        }
                    }

                    candidate.m_Position = MathUtils.Position(curve.m_Bezier, curvePos);
                    var tangent = MathUtils.Tangent(curve.m_Bezier, curvePos);
                    candidate.m_Direction = tangent.xz;
                    MathUtils.TryNormalize(ref candidate.m_Direction);

                    float snapPrio = 1f;
                    if ((m_NetGeometryData.m_Flags & Game.Net.GeometryFlags.StrictNodes) != 0 && dist <= halfWidth)
                        snapPrio = 2f;

                    candidate.m_SnapPriority = ToolUtils.CalculateSnapPriority(snapPrio, 1f, 1f, controlPoint.m_HitPosition, candidate.m_Position, candidate.m_Direction);
                    ToolUtils.AddSnapPosition(ref m_BestSnapPosition, candidate);

                    var snapLineFlags = (m_NetGeometryData.m_Flags & Game.Net.GeometryFlags.StrictNodes) == 0
                        ? SnapLineFlags.ExtendedCurve
                        : (SnapLineFlags)0;
                    ToolUtils.AddSnapLine(ref m_BestSnapPosition, m_SnapLines, new SnapLine(candidate, curve.m_Bezier, snapLineFlags, 1f));
                    snapAdded = true;
                }

                /// <summary>
                ///     Snaps to buildable net areas within an edge segment (e.g. the buildable
                ///     strip along a highway's service road). Produces a snap candidate that
                ///     locks to the area's lateral offset curve.
                /// </summary>
                private bool SnapSegmentAreas(ControlPoint controlPoint, NetCompositionData compositionData, DynamicBuffer<NetCompositionArea> areas, Segment segment, ref bool snapAdded) {
                    bool hasArea = false;
                    for (int i = 0; i < areas.Length; i++) {
                        var area = areas[i];
                        if ((area.m_Flags & NetAreaFlags.Buildable) == 0)
                            continue;

                        float areaHalfWidth = area.m_Width * 0.51f;
                        if (m_NetGeometryData.m_DefaultWidth * 0.5f >= areaHalfWidth)
                            continue;

                        hasArea = true;
                        var areaCurve = MathUtils.Lerp(segment.m_Left, segment.m_Right, area.m_Position.x / compositionData.m_Width + 0.5f);
                        float areaT;
                        MathUtils.Distance(areaCurve.xz, controlPoint.m_HitPosition.xz, out areaT);

                        var candidate = controlPoint;
                        candidate.m_Position = MathUtils.Position(areaCurve, areaT);
                        candidate.m_Direction = math.normalizesafe(MathUtils.Tangent(areaCurve, areaT).xz);
                        if ((area.m_Flags & NetAreaFlags.Invert) != 0)
                            candidate.m_Direction = -candidate.m_Direction;

                        candidate.m_Position.y += area.m_Position.y;
                        candidate.m_Rotation = ToolUtils.CalculateRotation(candidate.m_Direction);
                        candidate.m_SnapPriority = ToolUtils.CalculateSnapPriority(1f, 1f, 1f, controlPoint.m_HitPosition, candidate.m_Position, candidate.m_Direction);

                        ToolUtils.AddSnapPosition(ref m_BestSnapPosition, candidate);

                        var snapLineFlags = (m_NetGeometryData.m_Flags & Game.Net.GeometryFlags.StrictNodes) == 0
                            ? SnapLineFlags.ExtendedCurve
                            : (SnapLineFlags)0;
                        ToolUtils.AddSnapLine(ref m_BestSnapPosition, m_SnapLines, new SnapLine(candidate, areaCurve, snapLineFlags, 1f));
                        snapAdded = true;
                    }
                    return hasArea;
                }

                /// <summary>
                ///     Emits guide lines extending from an edge's start and end nodes.
                ///     Guide lines are directional rays along the edge's tangent at each endpoint,
                ///     plus perpendicular rays at dead-end nodes (nodes with only one connected edge).
                ///     These allow the player to align placement along or perpendicular to nearby roads.
                /// </summary>
                public void HandleGuideLines(Entity entity) {
                    if (!m_CurveData.HasComponent(entity))
                        return;

                    var prefabRef = m_PrefabRefData[entity];
                    var netData = m_PrefabNetData[prefabRef.m_Prefab];
                    NetGeometryData otherGeom = default;
                    if (m_PrefabGeometryData.HasComponent(prefabRef.m_Prefab))
                        otherGeom = m_PrefabGeometryData[prefabRef.m_Prefab];

                    if (!NetUtils.CanConnect(m_NetData, netData))
                        return;

                    var curve = m_CurveData[entity];
                    var edge = m_EdgeData[entity];

                    var startDir = -MathUtils.StartTangent(curve.m_Bezier).xz;
                    var endDir = MathUtils.EndTangent(curve.m_Bezier).xz;
                    bool hasStart = MathUtils.TryNormalize(ref startDir);
                    bool hasEnd = MathUtils.TryNormalize(ref endDir);

                    // Check if start/end nodes are dead-ends (single connected edge)
                    bool startDeadEnd = hasStart;
                    if (hasStart) {
                        var connEdges = m_ConnectedEdges[edge.m_Start];
                        for (int i = 0; i < connEdges.Length; i++) {
                            if (connEdges[i].m_Edge != entity) {
                                var otherEdge = m_EdgeData[connEdges[i].m_Edge];
                                if (otherEdge.m_Start == edge.m_Start || otherEdge.m_End == edge.m_Start) {
                                    startDeadEnd = false;
                                    break;
                                }
                            }
                        }
                    }

                    bool endDeadEnd = hasEnd;
                    if (hasEnd) {
                        var connEdges = m_ConnectedEdges[edge.m_End];
                        for (int i = 0; i < connEdges.Length; i++) {
                            if (connEdges[i].m_Edge != entity) {
                                var otherEdge = m_EdgeData[connEdges[i].m_Edge];
                                if (otherEdge.m_Start == edge.m_End || otherEdge.m_End == edge.m_End) {
                                    endDeadEnd = false;
                                    break;
                                }
                            }
                        }
                    }

                    if (!hasStart && !hasEnd)
                        return;

                    // Emit guide lines from start node
                    if (hasStart) {
                        var startPos = curve.m_Bezier.a;
                        var segment = new Line3.Segment(startPos, startPos);
                        segment.b.xz += startDir * m_GuideLength;

                        float t;
                        if (MathUtils.Distance(segment.xz, m_ControlPoint.m_HitPosition.xz, out t) < m_SnapDistance) {
                            var candidate = m_ControlPoint;
                            candidate.m_OriginalEntity = Entity.Null;
                            candidate.m_Position = MathUtils.Position(segment, t);
                            candidate.m_Direction = startDir;
                            candidate.m_SnapPriority = ToolUtils.CalculateSnapPriority(0f, 1f, 0.1f, m_ControlPoint.m_HitPosition, candidate.m_Position, candidate.m_Direction);
                            ToolUtils.AddSnapPosition(ref m_BestSnapPosition, candidate);
                            ToolUtils.AddSnapLine(ref m_BestSnapPosition, m_SnapLines, new SnapLine(candidate, NetUtils.StraightCurve(segment.a, segment.b), SnapLineFlags.GuideLine, 0.1f));
                        }

                        // Perpendicular guides at dead-end start nodes
                        if (startDeadEnd) {
                            var perpRight = new Line3.Segment(startPos, startPos);
                            perpRight.b.xz += MathUtils.Right(startDir) * m_GuideLength;
                            float tR;
                            if (MathUtils.Distance(perpRight.xz, m_ControlPoint.m_HitPosition.xz, out tR) < m_SnapDistance) {
                                var c = m_ControlPoint;
                                c.m_OriginalEntity = Entity.Null;
                                c.m_Position = MathUtils.Position(perpRight, tR);
                                c.m_Direction = MathUtils.Right(startDir);
                                c.m_SnapPriority = ToolUtils.CalculateSnapPriority(0f, 1f, 0.1f, m_ControlPoint.m_HitPosition, c.m_Position, c.m_Direction);
                                ToolUtils.AddSnapPosition(ref m_BestSnapPosition, c);
                                ToolUtils.AddSnapLine(ref m_BestSnapPosition, m_SnapLines, new SnapLine(c, NetUtils.StraightCurve(perpRight.a, perpRight.b), SnapLineFlags.GuideLine, 0.1f));
                            }

                            var perpLeft = new Line3.Segment(startPos, startPos);
                            perpLeft.b.xz += MathUtils.Left(startDir) * m_GuideLength;
                            float tL;
                            if (MathUtils.Distance(perpLeft.xz, m_ControlPoint.m_HitPosition.xz, out tL) < m_SnapDistance) {
                                var c = m_ControlPoint;
                                c.m_OriginalEntity = Entity.Null;
                                c.m_Position = MathUtils.Position(perpLeft, tL);
                                c.m_Direction = MathUtils.Left(startDir);
                                c.m_SnapPriority = ToolUtils.CalculateSnapPriority(0f, 1f, 0.1f, m_ControlPoint.m_HitPosition, c.m_Position, c.m_Direction);
                                ToolUtils.AddSnapPosition(ref m_BestSnapPosition, c);
                                ToolUtils.AddSnapLine(ref m_BestSnapPosition, m_SnapLines, new SnapLine(c, NetUtils.StraightCurve(perpLeft.a, perpLeft.b), SnapLineFlags.GuideLine, 0.1f));
                            }
                        }
                    }

                    // Emit guide lines from end node
                    if (hasEnd) {
                        var endPos = curve.m_Bezier.d;
                        var segment = new Line3.Segment(endPos, endPos);
                        segment.b.xz += endDir * m_GuideLength;

                        float t;
                        if (MathUtils.Distance(segment.xz, m_ControlPoint.m_HitPosition.xz, out t) < m_SnapDistance) {
                            var candidate = m_ControlPoint;
                            candidate.m_OriginalEntity = Entity.Null;
                            candidate.m_Position = MathUtils.Position(segment, t);
                            candidate.m_Direction = endDir;
                            candidate.m_SnapPriority = ToolUtils.CalculateSnapPriority(0f, 1f, 0.1f, m_ControlPoint.m_HitPosition, candidate.m_Position, candidate.m_Direction);
                            ToolUtils.AddSnapPosition(ref m_BestSnapPosition, candidate);
                            ToolUtils.AddSnapLine(ref m_BestSnapPosition, m_SnapLines, new SnapLine(candidate, NetUtils.StraightCurve(segment.a, segment.b), SnapLineFlags.GuideLine, 0.1f));
                        }

                        if (endDeadEnd) {
                            var perpRight = new Line3.Segment(endPos, endPos);
                            perpRight.b.xz += MathUtils.Right(endDir) * m_GuideLength;
                            float tR;
                            if (MathUtils.Distance(perpRight.xz, m_ControlPoint.m_HitPosition.xz, out tR) < m_SnapDistance) {
                                var c = m_ControlPoint;
                                c.m_OriginalEntity = Entity.Null;
                                c.m_Position = MathUtils.Position(perpRight, tR);
                                c.m_Direction = MathUtils.Right(endDir);
                                c.m_SnapPriority = ToolUtils.CalculateSnapPriority(0f, 1f, 0.1f, m_ControlPoint.m_HitPosition, c.m_Position, c.m_Direction);
                                ToolUtils.AddSnapPosition(ref m_BestSnapPosition, c);
                                ToolUtils.AddSnapLine(ref m_BestSnapPosition, m_SnapLines, new SnapLine(c, NetUtils.StraightCurve(perpRight.a, perpRight.b), SnapLineFlags.GuideLine, 0.1f));
                            }

                            var perpLeft = new Line3.Segment(endPos, endPos);
                            perpLeft.b.xz += MathUtils.Left(endDir) * m_GuideLength;
                            float tL;
                            if (MathUtils.Distance(perpLeft.xz, m_ControlPoint.m_HitPosition.xz, out tL) < m_SnapDistance) {
                                var c = m_ControlPoint;
                                c.m_OriginalEntity = Entity.Null;
                                c.m_Position = MathUtils.Position(perpLeft, tL);
                                c.m_Direction = MathUtils.Left(endDir);
                                c.m_SnapPriority = ToolUtils.CalculateSnapPriority(0f, 1f, 0.1f, m_ControlPoint.m_HitPosition, c.m_Position, c.m_Direction);
                                ToolUtils.AddSnapPosition(ref m_BestSnapPosition, c);
                                ToolUtils.AddSnapLine(ref m_BestSnapPosition, m_SnapLines, new SnapLine(c, NetUtils.StraightCurve(perpLeft.a, perpLeft.b), SnapLineFlags.GuideLine, 0.1f));
                            }
                        }
                    }
                }
            }

            // ═══════════════════════════════════════════════════════════════════════
            // ObjectIterator — snaps to building lot edges
            // ═══════════════════════════════════════════════════════════════════════

            /// <summary>
            ///     Quad-tree iterator that visits static objects (buildings) and produces snap
            ///     candidates for:
            ///     <list type="bullet">
            ///         <item><b>Object side</b> — snaps to the four lot edges of rectangular buildings.
            ///             The lot is expanded by <see cref="m_ObjectSnapOffset"/> to catch near-misses.</item>
            ///         <item><b>Owner node</b> — if the object owns a net node (e.g. a bus stop),
            ///             snaps to that node's position (delegated to <see cref="SnapToNode"/>).</item>
            ///     </list>
            /// </summary>
            private struct ObjectIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ> {
                public Bounds3 m_Bounds;
                public SnapOption m_SnapFlags;
                public float m_MaxDistance;
                public float m_NetSnapOffset;
                public float m_ObjectSnapOffset;
                public NetData m_NetData;
                public NetGeometryData m_NetGeometryData;
                public ControlPoint m_ControlPoint;
                public ControlPoint m_BestSnapPosition;
                public NativeList<SnapLine> m_SnapLines;
                public ComponentLookup<Owner> m_OwnerData;
                public ComponentLookup<Curve> m_CurveData;
                public ComponentLookup<Game.Net.Node> m_NodeData;
                public ComponentLookup<Game.Objects.Transform> m_TransformData;
                public ComponentLookup<PrefabRef> m_PrefabRefData;
                public ComponentLookup<BuildingData> m_BuildingData;
                public ComponentLookup<ObjectGeometryData> m_ObjectGeometryData;
                public ComponentLookup<NetData> m_PrefabNetData;
                public ComponentLookup<NetGeometryData> m_PrefabGeometryData;
                public BufferLookup<ConnectedEdge> m_ConnectedEdges;

                public bool Intersect(QuadTreeBoundsXZ bounds) {
                    return MathUtils.Intersect(bounds.m_Bounds, m_Bounds);
                }

                public void Iterate(QuadTreeBoundsXZ bounds, Entity entity) {
                    if (!MathUtils.Intersect(bounds.m_Bounds, m_Bounds))
                        return;

                    if ((m_SnapFlags & SnapOption.ExistingGeometry) != SnapOption.None && m_OwnerData.HasComponent(entity)) {
                        var owner = m_OwnerData[entity];
                        if (m_NodeData.HasComponent(owner.m_Owner))
                            SnapToNode(owner.m_Owner);
                    }

                    if ((m_SnapFlags & SnapOption.ObjectSide) != SnapOption.None)
                        SnapObjectSide(entity);
                }

                /// <summary>
                ///     Snaps to a standalone net node owned by an object. Skips nodes that
                ///     are already connected to edges (those are handled by the NetIterator)
                ///     and nodes that don't support connection to the placed prefab.
                /// </summary>
                private void SnapToNode(Entity entity) {
                    if (entity == m_ControlPoint.m_OriginalEntity && (m_SnapFlags & SnapOption.ExistingGeometry) != SnapOption.None)
                        return;
                    // Skip nodes with connected edges — the NetIterator handles those
                    if (m_ConnectedEdges.HasBuffer(entity) && m_ConnectedEdges[entity].Length > 0)
                        return;
                    if (!m_NodeData.HasComponent(entity))
                        return;

                    var node = m_NodeData[entity];
                    var prefabRef = m_PrefabRefData[entity];
                    if (!m_PrefabNetData.HasComponent(prefabRef.m_Prefab))
                        return;
                    if (!NetUtils.CanConnect(m_PrefabNetData[prefabRef.m_Prefab], m_NetData))
                        return;

                    var candidate = m_ControlPoint;
                    candidate.m_OriginalEntity = entity;
                    candidate.m_Position = node.m_Position;
                    candidate.m_Direction = math.mul(node.m_Rotation, new float3(0f, 0f, 1f)).xz;
                    MathUtils.TryNormalize(ref candidate.m_Direction);

                    float dist = math.distance(node.m_Position.xz, m_ControlPoint.m_HitPosition.xz);
                    float snapRadius = m_NetGeometryData.m_DefaultWidth * 0.5f;
                    if (m_PrefabGeometryData.TryGetComponent(prefabRef.m_Prefab, out var otherGeom))
                        snapRadius += otherGeom.m_DefaultWidth * 0.5f;

                    if (dist >= snapRadius + m_NetSnapOffset)
                        return;

                    float snapPrio = 1f;
                    if ((m_NetGeometryData.m_Flags & Game.Net.GeometryFlags.StrictNodes) != 0 && dist <= snapRadius)
                        snapPrio = 2f;

                    candidate.m_SnapPriority = ToolUtils.CalculateSnapPriority(snapPrio, 1f, 1f, m_ControlPoint.m_HitPosition, candidate.m_Position, candidate.m_Direction);
                    ToolUtils.AddSnapPosition(ref m_BestSnapPosition, candidate);
                }

                /// <summary>
                ///     Snaps to the four edges of a rectangular building lot. Circular
                ///     buildings and non-building objects are skipped.
                /// </summary>
                private void SnapObjectSide(Entity entity) {
                    if (!m_TransformData.HasComponent(entity))
                        return;

                    var transform = m_TransformData[entity];
                    var prefabRef = m_PrefabRefData[entity];
                    if (!m_ObjectGeometryData.HasComponent(prefabRef.m_Prefab))
                        return;

                    var objectGeom = m_ObjectGeometryData[prefabRef.m_Prefab];
                    if ((objectGeom.m_Flags & Game.Objects.GeometryFlags.Circular) != Game.Objects.GeometryFlags.None)
                        return;

                    if (!m_BuildingData.HasComponent(prefabRef.m_Prefab))
                        return;

                    var lotSize = m_BuildingData[prefabRef.m_Prefab].m_LotSize;
                    objectGeom.m_Bounds.min.xz = (float2)lotSize * -4f;
                    objectGeom.m_Bounds.max.xz = (float2)lotSize * 4f;
                    objectGeom.m_Bounds.min.xz -= m_ObjectSnapOffset;
                    objectGeom.m_Bounds.max.xz += m_ObjectSnapOffset;

                    var quad = ObjectUtils.CalculateBaseCorners(transform.m_Position, transform.m_Rotation, objectGeom.m_Bounds);
                    CheckLine(quad.ab);
                    CheckLine(quad.bc);
                    CheckLine(quad.cd);
                    CheckLine(quad.da);
                }

                /// <summary>
                ///     Tests a single lot edge segment for proximity to the hit position.
                ///     Produces a snap candidate aligned to the edge's tangent direction.
                /// </summary>
                private void CheckLine(Line3.Segment line) {
                    float t;
                    if (MathUtils.Distance(line.xz, m_ControlPoint.m_HitPosition.xz, out t) < m_MaxDistance) {
                        var candidate = m_ControlPoint;
                        candidate.m_Direction = math.normalizesafe(MathUtils.Tangent(line.xz));
                        candidate.m_Position = MathUtils.Position(line, t);
                        candidate.m_SnapPriority = ToolUtils.CalculateSnapPriority(1f, 1f, 1f, m_ControlPoint.m_HitPosition, candidate.m_Position, candidate.m_Direction);

                        if (m_CurveData.HasComponent(m_ControlPoint.m_OriginalEntity))
                            MathUtils.Distance(m_CurveData[m_ControlPoint.m_OriginalEntity].m_Bezier.xz, candidate.m_Position.xz, out candidate.m_CurvePosition);
                        else if (!m_NodeData.HasComponent(m_ControlPoint.m_OriginalEntity))
                            candidate.m_OriginalEntity = Entity.Null;

                        ToolUtils.AddSnapPosition(ref m_BestSnapPosition, candidate);
                        ToolUtils.AddSnapLine(ref m_BestSnapPosition, m_SnapLines, new SnapLine(candidate, NetUtils.StraightCurve(line.a, line.b), SnapLineFlags.Secondary, 1f));
                    }
                }
            }

            // ═══════════════════════════════════════════════════════════════════════
            // ZoneIterator — snaps to visible zone cells
            // ═══════════════════════════════════════════════════════════════════════

            /// <summary>
            ///     Quad-tree iterator that finds the closest visible zone cell to the cursor.
            ///     Does not produce snap candidates directly — it only finds the best cell
            ///     position and direction. The caller (<see cref="HandleZoneGrid"/>) uses
            ///     these to project the hit onto the zone grid and create the snap candidate.
            /// </summary>
            private struct ZoneIterator : INativeQuadTreeIterator<Entity, Bounds2>, IUnsafeQuadTreeIterator<Entity, Bounds2> {
                public Bounds2 m_Bounds;
                public float2  m_HitPosition;
                public float3  m_BestPosition;
                public float2  m_BestDirection;
                public float   m_BestDistance;
                public ComponentLookup<Block> m_ZoneBlockData;
                public BufferLookup<Cell> m_ZoneCells;

                public bool Intersect(Bounds2 bounds) {
                    return MathUtils.Intersect(bounds, m_Bounds);
                }

                /// <summary>
                ///     Visits a zone block entity. First checks the cell directly under the
                ///     cursor; if it isn't visible, falls back to scanning all cells in the
                ///     block for the closest visible one.
                /// </summary>
                public void Iterate(Bounds2 bounds, Entity entity) {
                    if (!MathUtils.Intersect(bounds, m_Bounds))
                        return;

                    var block = m_ZoneBlockData[entity];
                    var cells = m_ZoneCells[entity];

                    // Fast path: check the cell directly under the cursor
                    var cellIdx = math.clamp(ZoneUtils.GetCellIndex(block, m_HitPosition), 0, block.m_Size - 1);
                    var cellPos = ZoneUtils.GetCellPosition(block, cellIdx);
                    var dist = math.distance(cellPos.xz, m_HitPosition);

                    if (dist >= m_BestDistance)
                        return;

                    if ((cells[cellIdx.x + cellIdx.y * block.m_Size.x].m_State & CellFlags.Visible) != CellFlags.None) {
                        m_BestPosition = cellPos;
                        m_BestDirection = block.m_Direction;
                        m_BestDistance = dist;
                        return;
                    }

                    // Slow path: scan all cells in this block for the closest visible one
                    for (int y = 0; y < block.m_Size.y; y++) {
                        for (int x = 0; x < block.m_Size.x; x++) {
                            if ((cells[x + y * block.m_Size.x].m_State & CellFlags.Visible) != CellFlags.None) {
                                cellPos = ZoneUtils.GetCellPosition(block, new int2(x, y));
                                dist = math.distance(cellPos.xz, m_HitPosition);
                                if (dist < m_BestDistance) {
                                    m_BestPosition = cellPos;
                                    m_BestDirection = block.m_Direction;
                                    m_BestDistance = dist;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
