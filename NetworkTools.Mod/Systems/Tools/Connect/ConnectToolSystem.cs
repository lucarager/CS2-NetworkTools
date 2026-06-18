namespace NetworkTools.Systems.Tools.Connect {
    using Game.Prefabs;

    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Rendering;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.Handles;
    using NetworkTools.Systems.Tools.Parameters;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Tool system for connecting two nodes with a curve or loop.
    /// </summary>
    public partial class NT_ConnectToolSystem : NT_BaseToolSystem, IToolPrefabProvider, INodeSelectionProvider, IManualApplyProvider {
        /// <inheritdoc />
        public override string toolID => "ConnectTool";

        /// <inheritdoc />
        public override bool SupportsAnarchy => true;

        // ── Parameters

        public NetPrefabParameter         NetPrefab  = new("connect.netPrefab");
        public EnumParameter<ConnectMode> Mode       = new("connect.mode", ConnectMode.SimpleCurve, label: "NetworkTools.UI.Common.Mode");

        // Shared (from node selection)
        public Float3Parameter StartPosition  = new("connect.startPosition");
        public Float3Parameter EndPosition    = new("connect.endPosition");
        public Float3Parameter StartDirection = new("connect.startDirection", modes: (int)ConnectMode.Loop) {
            Handles = new IHandleSpec<float3>[] {
                new RotationHandle {
                    DependsOn                   = new Dependency[] { nameof(StartPosition) },
                    RenderConnectionTo          = nameof(StartPosition),
                    Style                       = HandleTypeFlags.Primary,
                    ReferenceDirectionFromValue = true
                }
            }
        };
        public Float3Parameter EndDirection = new("connect.endDirection", modes: (int)ConnectMode.Loop) {
            Handles = new IHandleSpec<float3>[] {
                new RotationHandle {
                    DependsOn                   = new Dependency[] { nameof(EndPosition) },
                    RenderConnectionTo          = nameof(EndPosition),
                    Style                       = HandleTypeFlags.Primary,
                    ReferenceDirectionFromValue = true
                }
            }
        };

        // Simple Curve (from generator init + handle drags)
        public Float3Parameter CurveStartPointPosition = new(
            "connect.curveStartPointPosition",
            modes: (int)ConnectMode.SimpleCurve) {
            Handles = new IHandleSpec<float3>[] { new PositionHandle() }
        };
        public Float3Parameter CurveStartControlPointPosition = new(
            "connect.curveStartControlPointPosition",
            modes: (int)ConnectMode.SimpleCurve) {
            Handles = new IHandleSpec<float3>[] {
                new PositionHandle {
                    Style            = HandleTypeFlags.BezierControlPoint,
                    DependsOn        = new Dependency[] { nameof(CurveStartPointPosition) },
                    Size             = NT_Dimensions.HANDLE_AXIS_CIRCLE_RADIUS,
                    ConstraintAxis   = t => ((NT_ConnectToolSystem)t).StartDirection.Value,
                    ConstraintOrigin = t => ((NT_ConnectToolSystem)t).StartPosition.Value,
                }
            }
        };
        public Float3Parameter CurveEndControlPointPosition = new(
            "connect.curveEndControlPointPosition",
            modes: (int)ConnectMode.SimpleCurve) {
            Handles = new IHandleSpec<float3>[] {
                new PositionHandle {
                    Style            = HandleTypeFlags.BezierControlPoint,
                    DependsOn        = new Dependency[] { nameof(CurveEndPointPosition) },
                    Size             = NT_Dimensions.HANDLE_AXIS_CIRCLE_RADIUS,
                    ConstraintAxis   = t => ((NT_ConnectToolSystem)t).EndDirection.Value,
                    ConstraintOrigin = t => ((NT_ConnectToolSystem)t).EndPosition.Value,
                }
            }
        };
        public Float3Parameter CurveEndPointPosition = new(
            "connect.curveEndPointPosition",
            modes: (int)ConnectMode.SimpleCurve) {
            Handles = new IHandleSpec<float3>[] { new PositionHandle() }
        };

        // Complex Curve
        public Float3Parameter ComplexStartPointPosition = new(
            "connect.complexStartPointPosition",
            modes: (int)ConnectMode.ComplexCurve) {
            Handles = new IHandleSpec<float3>[] { new PositionHandle() }
        };
        public Float3Parameter ComplexStartControlPointPosition = new(
            "connect.complexStartControlPointPosition",
            modes: (int)ConnectMode.ComplexCurve) {
            Handles = new IHandleSpec<float3>[] {
                new PositionHandle {
                    Style            = HandleTypeFlags.BezierControlPoint,
                    DependsOn        = new Dependency[] { nameof(ComplexStartPointPosition) },
                    Size             = NT_Dimensions.HANDLE_AXIS_CIRCLE_RADIUS,
                    ConstraintAxis   = t => ((NT_ConnectToolSystem)t).StartDirection.Value,
                    ConstraintOrigin = t => ((NT_ConnectToolSystem)t).StartPosition.Value,
                }
            }
        };
        public Float3Parameter ComplexEndControlPointPosition = new(
            "connect.complexEndControlPointPosition",
            modes: (int)ConnectMode.ComplexCurve) {
            Handles = new IHandleSpec<float3>[] {
                new PositionHandle {
                    Style            = HandleTypeFlags.BezierControlPoint,
                    DependsOn        = new Dependency[] { nameof(ComplexEndPointPosition) },
                    Size             = NT_Dimensions.HANDLE_AXIS_CIRCLE_RADIUS,
                    ConstraintAxis   = t => ((NT_ConnectToolSystem)t).EndDirection.Value,
                    ConstraintOrigin = t => ((NT_ConnectToolSystem)t).EndPosition.Value,
                }
            }
        };
        public Float3Parameter ComplexEndPointPosition = new(
            "connect.complexEndPointPosition",
            modes: (int)ConnectMode.ComplexCurve) {
            Handles = new IHandleSpec<float3>[] { new PositionHandle() }
        };
        public Float3Parameter ComplexMidPosition = new(
            "connect.complexMidPosition",
            modes: (int)ConnectMode.ComplexCurve) {
            Handles = new IHandleSpec<float3>[] { new PositionHandle() }
        };
        public Float3Parameter ComplexMidRotation = new(
            "connect.complexMidRotation",
            modes: (int)ConnectMode.ComplexCurve) {
            Handles = new IHandleSpec<float3>[] {
                new RotationHandle {
                    DependsOn                   = new Dependency[] { nameof(ComplexMidPosition) },
                    RenderConnectionTo          = nameof(ComplexMidPosition),
                    Style                       = HandleTypeFlags.Primary,
                    ReferenceDirectionFromValue = true
                }
            }
        };

        // Loop
        public FloatParameter LoopRadiusFactor = new("connect.loopRadiusFactor", 0.5f, 0f, 1f,
            modes: (int)ConnectMode.Loop,
            label: "NetworkTools.UI.Connect.LoopRadius",
            fractionDigits: 2) {
            Handles = new IHandleSpec<float>[] {
                new AxisHandle {
                    StartPoint = tool => {
                        var ct = (NT_ConnectToolSystem)tool;
                        LoopGenerator.ComputeLoopAxis(
                            ct.StartPosition.Value, ct.StartDirection.Value,
                            ct.EndPosition.Value, ct.EndDirection.Value,
                            out var nc, out _, out _);
                        return nc;
                    },
                    EndPoint = tool => {
                        var ct = (NT_ConnectToolSystem)tool;
                        LoopGenerator.ComputeLoopAxis(
                            ct.StartPosition.Value, ct.StartDirection.Value,
                            ct.EndPosition.Value, ct.EndDirection.Value,
                            out var nc, out var axisDir, out _);
                        return LoopGenerator.ComputeMaxCenter(nc, axisDir,
                            ct.StartPosition.Value, ct.EndPosition.Value);
                    },
                    YOffset = 1f,
                }
            }
        };
        public EnumParameter<LoopArcSide> LoopArc = new("connect.loopArcSide", LoopArcSide.Outer,
            modes: (int)ConnectMode.Loop,
            label: "NetworkTools.UI.Connect.LoopArcSide");

        /// <inheritdoc />
        protected override int GetActiveModeFlag() => (int)Mode.Value;

        // ── Non-parameter state

        /// <summary>
        ///     Caches the last hit position for tool-specific use.
        /// </summary>
        private float3 m_LastHitPosition;

        /// <summary>
        ///     List of user-selected node entities that define path endpoints.
        /// </summary>
        protected NativeList<Entity> m_SelectedNodes;

        /// <summary>
        ///     Gets the current selection state based on the number of selected nodes.
        /// </summary>
        public SelectionState CurrentSelectionState =>
            m_SelectedNodes.Length switch {
                0 => SelectionState.NoSelection,
                1 => SelectionState.StartNodeSelected,
                _ => SelectionState.EndNodeSelected
            };

        /// <summary>
        ///     Gets a value indicating whether a complete path is selected (2+ nodes).
        /// </summary>
        public bool HasCompletePath => m_SelectedNodes.Length >= 2;

        /// <summary>
        ///     Gets the start node of the selection, or Entity.Null if none selected.
        /// </summary>
        public Entity StartNode => m_SelectedNodes.Length > 0 ? m_SelectedNodes[0] : Entity.Null;

        /// <summary>
        ///     Gets the end node of the selection, or Entity.Null if less than 2 nodes selected.
        /// </summary>
        public Entity EndNode => m_SelectedNodes.Length >= 2 ? m_SelectedNodes[^1] : Entity.Null;

        /// <inheritdoc />
        public Entity[] GetSelectedNodes() {
            return m_SelectedNodes.IsCreated
                       ? m_SelectedNodes.ToArray(Allocator.Temp).ToArray()
                       : System.Array.Empty<Entity>();
        }
    }
}
