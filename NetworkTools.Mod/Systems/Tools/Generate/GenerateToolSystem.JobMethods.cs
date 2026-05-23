namespace NetworkTools.Systems.Tools.Generate {
    using Game.Common;
    using Game.Net;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;
    using Game.Zones;

    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    ///     Job scheduling and output methods for <see cref="NT_GenerateToolSystem"/>.
    /// </summary>
    public partial class NT_GenerateToolSystem {
        /// <summary>
        ///     Builds a Burst-compatible snapshot struct from the current parameter values.
        /// </summary>
        internal GenerateJobConfig BuildJobConfig() {
            return new GenerateJobConfig {
                Position              = Position.Value,
                StartDirection        = quaternion.LookRotationSafe(Rotation.Value, math.up()),
                GridXSpacing          = GridXSpacing.Value,
                GridZSpacing          = GridZSpacing.Value,
                GridXNum                = GridXNum.Value,
                GridZNum                = GridZNum.Value,
                AltPrefabX              = AlternatingNetworkPrefabX.Value,
                AltNetPrefabXEntity     = AltNetPrefabX.NetPrefabEntity,
                AltNetLanePrefabXEntity = AltNetPrefabX.NetLanePrefabEntity,
                AltEveryX               = AltEveryX.Value,
                AltPrefabZ              = AlternatingNetworkPrefabZ.Value,
                AltNetPrefabZEntity     = AltNetPrefabZ.NetPrefabEntity,
                AltNetLanePrefabZEntity = AltNetPrefabZ.NetLanePrefabEntity,
                AltEveryZ               = AltEveryZ.Value,
                CircleRadius          = CircleRadius.Value,
                OvalRadiusX           = OvalRadiusX.Value,
                OvalRadiusZ           = OvalRadiusZ.Value,
                Elevation             = Elevation.Value,
            };
        }

        private JobHandle ScheduleSnapJob(ControlPoint rawControlPoint, JobHandle inputDeps) {
            var netTree = m_NetSearchSystem.GetNetSearchTree(true, out var netDep);
            var objTree = m_ObjectSearchSystem.GetStaticSearchTree(true, out var objDep);
            var zoneTree = m_ZoneSearchSystem.GetSearchTree(true, out var zoneDep);

            JobHandle waterDep;
            var waterData = m_WaterSystem.GetSurfaceData(out waterDep);

            var snapJob = new SnapPlacementJob {
                m_ControlPoint   = rawControlPoint,
                m_SnapFlags      = SelectedSnaps,
                m_Prefab         = NetPrefab.NetPrefabEntity,
                m_Elevation      = Elevation.Value,

                m_NetSearchTree    = netTree,
                m_ObjectSearchTree = objTree,
                m_ZoneSearchTree   = zoneTree,

                m_TerrainHeightData = m_TerrainSystem.GetHeightData(false),
                m_WaterSurfaceData  = waterData,

                m_NodeData               = SystemAPI.GetComponentLookup<Game.Net.Node>(true),
                m_EdgeData               = SystemAPI.GetComponentLookup<Edge>(true),
                m_CurveData              = SystemAPI.GetComponentLookup<Curve>(true),
                m_OwnerData              = SystemAPI.GetComponentLookup<Owner>(true),
                m_RoadData               = SystemAPI.GetComponentLookup<Road>(true),
                m_CompositionData        = SystemAPI.GetComponentLookup<Composition>(true),
                m_EdgeGeometryData       = SystemAPI.GetComponentLookup<EdgeGeometry>(true),
                m_PrefabRefData          = SystemAPI.GetComponentLookup<PrefabRef>(true),
                m_PrefabNetData          = SystemAPI.GetComponentLookup<NetData>(true),
                m_PrefabGeometryData     = SystemAPI.GetComponentLookup<NetGeometryData>(true),
                m_PlaceableData          = SystemAPI.GetComponentLookup<PlaceableNetData>(true),
                m_PrefabCompositionData  = SystemAPI.GetComponentLookup<NetCompositionData>(true),
                m_RoadCompositionData    = SystemAPI.GetComponentLookup<RoadComposition>(true),
                m_TransformData          = SystemAPI.GetComponentLookup<Game.Objects.Transform>(true),
                m_BuildingData           = SystemAPI.GetComponentLookup<BuildingData>(true),
                m_ObjectGeometryData     = SystemAPI.GetComponentLookup<ObjectGeometryData>(true),
                m_ZoneBlockData          = SystemAPI.GetComponentLookup<Block>(true),
                m_LocalConnectData       = SystemAPI.GetComponentLookup<LocalConnectData>(true),

                m_ConnectedEdges         = SystemAPI.GetBufferLookup<ConnectedEdge>(true),
                m_ZoneCells              = SystemAPI.GetBufferLookup<Cell>(true),
                m_SubNets                = SystemAPI.GetBufferLookup<Game.Net.SubNet>(true),
                m_PrefabCompositionAreas = SystemAPI.GetBufferLookup<NetCompositionArea>(true),

                m_SnappedControlPoint = m_SnappedControlPoint,
                m_SnappedEntity       = m_SnappedEntity,
                m_SnapLines           = m_SnapLines,
            };

            var combined = JobHandle.CombineDependencies(inputDeps, netDep, objDep);
            combined = JobHandle.CombineDependencies(combined, zoneDep, waterDep);
            var handle = snapJob.Schedule(combined);

            m_NetSearchSystem.AddNetSearchTreeReader(handle);
            m_ObjectSearchSystem.AddStaticSearchTreeReader(handle);
            m_TerrainSystem.AddCPUHeightReader(handle);
            m_WaterSystem.AddSurfaceReader(handle);

            return handle;
        }

        private JobHandle ScheduleDefinitionsJob(JobHandle inputDeps, ToolOutputMode outputMode) {
            m_Log.Debug($"ScheduleDefinitionsJob: Mode={Mode.Value}");

            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);

            var isHoverPreview = m_SelectedControlPoint.value.Equals(default);
            var controlPoint = isHoverPreview ? m_HoveredControlPoint.value : m_SelectedControlPoint.value;

            if (controlPoint.Equals(default))
            {
                m_Log.Debug("No valid control point for definition generation. Skipping job scheduling.");
                return inputDeps;
            }

            var config = BuildJobConfig();
            config.BaselineElevation = controlPoint.m_Elevation;

            var jobHandle = new CreateDefinitionsJob {
                Mode                   = Mode.Value,
                Config                 = config,
                NetPrefabEntity        = NetPrefab.NetPrefabEntity,
                NetLanePrefabEntity    = NetPrefab.NetLanePrefabEntity,
                OutputMode             = outputMode,
                Seed                   = RandomSeed.Next(),

                NodeLookup             = SystemAPI.GetComponentLookup<Node>(true),
                CurveLookup            = SystemAPI.GetComponentLookup<Curve>(true),
                EdgeLookup             = SystemAPI.GetComponentLookup<Edge>(true),
                UpgradedLookup         = SystemAPI.GetComponentLookup<Upgraded>(true),
                PrefabRefLookup        = SystemAPI.GetComponentLookup<PrefabRef>(true),
                PseudoRandomSeedLookup = SystemAPI.GetComponentLookup<PseudoRandomSeed>(true),
                ConnectedEdgeLookup    = SystemAPI.GetBufferLookup<ConnectedEdge>(true),
                AggregatedLookup       = SystemAPI.GetComponentLookup<Aggregated>(true),
                NetGeometryDataLookup  = SystemAPI.GetComponentLookup<NetGeometryData>(true),
                ECB                    = m_Barrier.CreateCommandBuffer(),
            }.Schedule(inputDeps);
            m_Barrier.AddJobHandleForProducer(jobHandle);

            return jobHandle;
        }

        private JobHandle Update(JobHandle inputDeps) {
            if (!m_UpdateNeeded)
            {
                applyMode = ApplyMode.None;
                return inputDeps;
            }

            applyMode = ApplyMode.Clear;
            inputDeps = ScheduleDefinitionsJob(inputDeps, ToolOutputMode.Preview);

            m_UpdateNeeded = false;

            return inputDeps;
        }

        private JobHandle Clear(JobHandle inputDeps) {
            applyMode = ApplyMode.Clear;
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_Barrier, inputDeps);
            return inputDeps;
        }

        private JobHandle Apply(JobHandle inputDeps) {
            applyMode = ApplyMode.Apply;
            var jobHandle = ScheduleDefinitionsJob(inputDeps, ToolOutputMode.Apply);

            jobHandle.Complete();

            ResetToIdle();

            return jobHandle;
        }
    }
}
