namespace NetworkTools.Systems.Tools.Generate {
    using Colossal.Collections;

    using Game.Input;
    using Game.Tools;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.Handles;
    using NetworkTools.Systems.Tools.Parameters;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Tool system for generating networks.
    /// </summary>
    public partial class NT_GenerateToolSystem : NT_BaseToolSystem, IToolPrefabProvider, IManualApplyProvider {
        /// <inheritdoc />
        public override string toolID => "GenerateTool";

        /// <inheritdoc />
        public override bool SupportsAnarchy => true;

        /// <inheritdoc />
        public override TargetOption AvailableTargets => TargetOption.None;

        /// <inheritdoc />
        // TODO: Unhide once snap job is tested in-game
        public override SnapOption AvailableSnaps => SnapOption.None;

        // Shared
        public NetPrefabParameter NetPrefab = new("generate.netPrefab", modes: (int)GenerateMode.Grid | (int)GenerateMode.Circle | (int)GenerateMode.Oval);
        public Float3Parameter Position = new("generate.position", modes: (int)GenerateMode.Grid | (int)GenerateMode.Circle | (int)GenerateMode.Oval) {
            Handles = new IHandleSpec<float3>[] { new PositionHandle() }
        };

        public Float3Parameter Rotation = new("generate.rotation", new float3(0, 0, 1),
            modes: (int)GenerateMode.Grid | (int)GenerateMode.Oval) {
            Handles = new IHandleSpec<float3>[] {
                new RotationHandle {
                    Parent = nameof(Position),
                    Normal = new float3(0, 1, 0),
                    ReferenceDirection = new float3(0, 0, 1),
                }
            }
        };

        // Grid
        public EnumParameter<GenerateMode> Mode         = new("generate.mode", GenerateMode.Grid, label: "NetworkTools.UI.Common.Mode");
        public FloatParameter GridXSpacing = new("generate.gridXSpacing", 60f, 0f, 240f, modes: (int)GenerateMode.Grid, label: "NetworkTools.UI.Generate.XSpacing", fractionDigits: 0, numberType: NumberType.Distance);
        public FloatParameter GridZSpacing = new("generate.gridZSpacing", 60f, 0f, 240f, modes: (int)GenerateMode.Grid, label: "NetworkTools.UI.Generate.ZSpacing", fractionDigits: 0, numberType: NumberType.Distance);
        public IntParameter   GridXNum     = new("generate.gridXNum", 3, 2, 20, modes: (int)GenerateMode.Grid, label: "NetworkTools.UI.Generate.XCount", numberType: NumberType.Columns);
        public IntParameter   GridZNum     = new("generate.gridZNum", 3, 2, 20, modes: (int)GenerateMode.Grid, label: "NetworkTools.UI.Generate.ZCount", numberType: NumberType.Rows);
        public BoolParameter      AlternatingNetworkPrefabX = new("generate.altPrefabX", false, modes: (int)GenerateMode.Grid, label: "NetworkTools.UI.Generate.AltPrefabX");
        public NetPrefabParameter AltNetPrefabX             = new("generate.altNetPrefabX", modes: (int)GenerateMode.Grid, label: "NetworkTools.UI.Generate.AltNetPrefabX");
        public IntParameter       AltEveryX                 = new("generate.altEveryX", 2, 2, 20, modes: (int)GenerateMode.Grid, label: "NetworkTools.UI.Generate.AltEveryX", numberType: NumberType.Columns);
        public BoolParameter      AlternatingNetworkPrefabZ = new("generate.altPrefabZ", false, modes: (int)GenerateMode.Grid, label: "NetworkTools.UI.Generate.AltPrefabZ");
        public NetPrefabParameter AltNetPrefabZ             = new("generate.altNetPrefabZ", modes: (int)GenerateMode.Grid, label: "NetworkTools.UI.Generate.AltNetPrefabZ");
        public IntParameter       AltEveryZ                 = new("generate.altEveryZ", 2, 2, 20, modes: (int)GenerateMode.Grid, label: "NetworkTools.UI.Generate.AltEveryZ", numberType: NumberType.Rows);

        // Circle
        public FloatParameter CircleRadius = new("generate.circleRadius", 60f, 4f, 240f, modes: (int)GenerateMode.Circle, label: "NetworkTools.UI.Generate.Radius", fractionDigits: 0, numberType: NumberType.Distance);

        // Oval
        public FloatParameter OvalRadiusX = new("generate.ovalRadiusX", 80f, 4f, 240f, modes: (int)GenerateMode.Oval, label: "NetworkTools.UI.Generate.RadiusX", fractionDigits: 0, numberType: NumberType.Distance);
        public FloatParameter OvalRadiusZ = new("generate.ovalRadiusZ", 40f, 4f, 240f, modes: (int)GenerateMode.Oval, label: "NetworkTools.UI.Generate.RadiusZ", fractionDigits: 0, numberType: NumberType.Distance);

        // Elevation
        public FloatParameter Elevation = new("generate.elevation", 0f, -100f, 100f, modes: (int)GenerateMode.Grid | (int)GenerateMode.Circle | (int)GenerateMode.Oval, label: "NetworkTools.UI.Generate.Elevation", fractionDigits: 0, numberType: NumberType.Distance);
        public BoolParameter FollowTerrain = new("generate.followTerrain", true, modes: (int)GenerateMode.Grid | (int)GenerateMode.Circle | (int)GenerateMode.Oval, label: "NetworkTools.UI.Generate.FollowTerrain");

        /// <inheritdoc />
        protected override int GetActiveModeFlag() => (int)Mode.Value;

        // Search systems for snap job
        private Game.Net.SearchSystem m_NetSearchSystem;
        private Game.Objects.SearchSystem m_ObjectSearchSystem;
        private Game.Zones.SearchSystem m_ZoneSearchSystem;
        private Game.Simulation.WaterSystem m_WaterSystem;

        // Snap state
        private NativeList<SnapLine> m_SnapLines;
        private NativeValue<ControlPoint> m_SnappedControlPoint;
        private NativeValue<Entity> m_SnappedEntity;

        /// <summary>
        ///     Caches the last hit position for tool-specific use.
        /// </summary>
        private float3 m_LastHitPosition;

        /// <summary>
        ///     Hovered control point
        /// </summary>
        protected NativeValue<ControlPoint> m_HoveredControlPoint;

        /// <summary>
        ///     Selected control point
        /// </summary>
        protected NativeValue<ControlPoint> m_SelectedControlPoint;
    }
}
