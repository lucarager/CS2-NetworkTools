namespace NetworkTools.Systems.Tools.Connect {
    using Game.Prefabs;

    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.Handles;
    using NetworkTools.Systems.Tools.Parameters;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Tool system for connecting two nodes with a curve or loop.
    /// </summary>
    public partial class NT_ConnectToolSystem : NT_BaseToolSystem, IToolPrefabProvider, INetPrefabCachingProvider, INodeSelectionProvider, INetPrefabSelectionProvider, IManualApplyProvider {
        /// <inheritdoc />
        public override string toolID => "ConnectTool";

        /// <inheritdoc />
        public override TargetOption AvailableTargets => TargetOption.Road | TargetOption.Path;

        // ── Parameters

        public EnumParameter<ConnectMode> Mode       = new("connect.mode", ConnectMode.None);
        public FloatParameter             LoopRadius = new("connect.loopRadius", 50f, 1f, 500f, modes: (int)ConnectMode.Loop) {
            Handles = new IHandleSpec<float>[] {
                new CircleHandle {
                    Parent = nameof(LoopControlPointPosition),
                    Size = NT_Handle.SizeSecondary
                }
            }
        };

        // Shared (from node selection)
        public Float3Parameter StartPosition  = new("connect.startPosition");
        public Float3Parameter EndPosition    = new("connect.endPosition");
        public Float3Parameter StartDirection = new("connect.startDirection", modes: (int)ConnectMode.Loop) {
            Handles = new IHandleSpec<float3>[] {
                new RotationHandle {
                    Parent = nameof(StartPosition),
                    Style = HandleTypeFlags.Primary,
                    ReferenceDirectionFrom = nameof(StartDirection)
                }
            }
        };
        public Float3Parameter EndDirection = new("connect.endDirection", modes: (int)ConnectMode.Loop) {
            Handles = new IHandleSpec<float3>[] {
                new RotationHandle {
                    Parent = nameof(EndPosition),
                    Style = HandleTypeFlags.Primary,
                    ReferenceDirectionFrom = nameof(EndDirection)
                }
            }
        };

        // Curve (from generator init + handle drags)
        public Float3Parameter CurveStartPointPosition = new(
            "connect.curveStartPointPosition",
            modes: (int)ConnectMode.SimpleCurve | (int)ConnectMode.ComplexCurve) {
            Handles = new IHandleSpec<float3>[] { new PositionHandle() }
        };
        public Float3Parameter CurveStartControlPointPosition = new(
            "connect.curveStartControlPointPosition",
            modes: (int)ConnectMode.SimpleCurve | (int)ConnectMode.ComplexCurve) {
            Handles = new IHandleSpec<float3>[] {
                new PositionHandle {
                    Style  = HandleTypeFlags.BezierControlPoint,
                    Parent = nameof(CurveStartPointPosition),
                    Size = NT_Handle.SizeSecondary
                }
            }
        };
        public Float3Parameter CurveEndControlPointPosition = new(
            "connect.curveEndControlPointPosition",
            modes: (int)ConnectMode.SimpleCurve | (int)ConnectMode.ComplexCurve) {
            Handles = new IHandleSpec<float3>[] {
                new PositionHandle {
                    Style  = HandleTypeFlags.BezierControlPoint,
                    Parent = nameof(CurveEndPointPosition),
                    Size = NT_Handle.SizeSecondary
                }
            }
        };
        public Float3Parameter CurveEndPointPosition = new(
            "connect.curveEndPointPosition",
            modes: (int)ConnectMode.SimpleCurve | (int)ConnectMode.ComplexCurve) {
            Handles = new IHandleSpec<float3>[] { new PositionHandle() }
        };

        // Loop (from generator init + handle drags)
        public Float3Parameter LoopControlPointPosition = new("connect.loopControlPointPosition", modes: (int)ConnectMode.Loop) {
            Handles = new IHandleSpec<float3>[] { new PositionHandle() }
        };

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
