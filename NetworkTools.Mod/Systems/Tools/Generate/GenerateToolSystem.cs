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
        public override SnapOption AvailableSnaps => SnapOption.AllUsual;

        // ── Parameters: Shared ──────────────────────────────────────────────
        public EnumParameter<GenerateMode> Mode = new("generate.mode", GenerateMode.Grid, label: "NetworkTools.UI.Common.Mode");
        public NetPrefabParameter NetPrefab = new("generate.netPrefab", modes: (int)GenerateMode.Grid | (int)GenerateMode.Circle | (int)GenerateMode.Oval);
        public Float3Parameter Position = new("generate.position", modes: (int)GenerateMode.Grid | (int)GenerateMode.Circle | (int)GenerateMode.Oval) {
            Handles = new IHandleSpec<float3>[] { new PositionHandle { Snap = HandleSnap.WorldSnap() } }
        };

        public Float3Parameter Rotation = new("generate.rotation", new float3(0, 0, 1),
            modes: (int)GenerateMode.Grid | (int)GenerateMode.Oval);

        // ── Parameters: Grid ────────────────────────────────────────────────
        public Float3Parameter GridDirectionPoint = new("generate.gridDirPoint",
            modes: (int)GenerateMode.Grid) {
            Handles = new IHandleSpec<float3>[] {
                new PositionHandle {
                    DependsOn          = new Dependency[] { nameof(Position) },
                    RenderConnectionTo = nameof(Position),
                    Snap               = HandleSnap.WorldSnap()
                }
            }
        };

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

        // ── Parameters: Circle ──────────────────────────────────────────────
        public FloatParameter CircleRadius = new("generate.circleRadius", 60f, 4f, 240f, modes: (int)GenerateMode.Circle, label: "NetworkTools.UI.Generate.Radius", fractionDigits: 0, numberType: NumberType.Distance) {
            Handles = new IHandleSpec<float>[] {
                new CircleHandle {
                    DependsOn          = new Dependency[] { nameof(Position) },
                    RenderConnectionTo = nameof(Position),
                    Normal             = new float3(0, 1, 0),
                }
            }
        };

        // ── Parameters: Oval ────────────────────────────────────────────────
        // Depth-axis tip. Dragging it sets both the oval's depth (distance from Position) and its
        // orientation (direction from Position) — see InitializeFromSecondPoint. Width is a separate
        // slider on OvalRadiusX.
        public Float3Parameter OvalAxisPoint = new("generate.ovalAxisPoint",
            modes: (int)GenerateMode.Oval) {
            Handles = new IHandleSpec<float3>[] {
                new PositionHandle {
                    DependsOn          = new Dependency[] { nameof(Position) },
                    RenderConnectionTo = nameof(Position),
                    Snap               = HandleSnap.WorldSnap()
                }
            }
        };

        public FloatParameter OvalRadiusX = new("generate.ovalRadiusX", 80f, 4f, 240f, modes: (int)GenerateMode.Oval, label: "NetworkTools.UI.Generate.RadiusX", fractionDigits: 0, numberType: NumberType.Distance) {
            Handles = new IHandleSpec<float>[] {
                new AxisHandle {
                    DependsOn = new Dependency[] { nameof(Position), nameof(Rotation) },
                    StartPoint = tool => ((NT_GenerateToolSystem)tool).Position.Value,
                    EndPoint = tool => {
                        var t = (NT_GenerateToolSystem)tool;
                        var perp = math.cross(new float3(0, 1, 0), math.normalizesafe(t.Rotation.Value));
                        return t.Position.Value + perp;
                    },
                }
            }
        };

        // Depth is owned by the OvalAxisPoint tip (drag distance = depth, drag direction = rotation),
        // so this radius is UI-only.
        public FloatParameter OvalRadiusZ = new("generate.ovalRadiusZ", 40f, 4f, 240f, modes: (int)GenerateMode.Oval, label: "NetworkTools.UI.Generate.RadiusZ", fractionDigits: 0, numberType: NumberType.Distance);

        // ── Parameters: Elevation ───────────────────────────────────────────
        public FloatParameter Elevation = new("generate.elevation", 0f, -100f, 100f, modes: (int)GenerateMode.Grid | (int)GenerateMode.Circle | (int)GenerateMode.Oval, label: "NetworkTools.UI.Generate.Elevation", fractionDigits: 0, numberType: NumberType.Distance);
        public BoolParameter FollowTerrain = new("generate.followTerrain", true, modes: (int)GenerateMode.Grid | (int)GenerateMode.Circle | (int)GenerateMode.Oval, label: "NetworkTools.UI.Generate.FollowTerrain");

        /// <inheritdoc />
        protected override int GetActiveModeFlag() => (int)Mode.Value;

        /// <inheritdoc />
        protected override Entity GetSnapPrefab() => NetPrefab.NetPrefabEntity;

        /// <inheritdoc />
        protected override float GetSnapElevation() => Elevation.Value;

        /// <summary>
        ///     Canonical control point storage. Count determines phase:
        ///     0 = Idle, 1 = Configuring, 2 = Ready.
        /// </summary>
        private NativeList<ControlPoint> m_ControlPoints;

        /// <summary>
        ///     Baseline elevation from the first control point's terrain hit.
        /// </summary>
        private float m_BaselineElevation;
    }
}
