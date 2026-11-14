namespace NetworkTools.Systems.Tools.Connect {
    using Colossal.Entities;

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

        /// <inheritdoc />
        public override bool TunnelAvailable {
            get {
                var prefab = NetPrefab.NetPrefabEntity;

                // Without a network picked, the tool lays the first selected node's own.
                if (prefab == Entity.Null && NetPrefab.NetLanePrefabEntity == Entity.Null) {
                    if (m_SelectedNodes.Length == 0
                        || !EntityManager.TryGetComponent<PrefabRef>(
                            m_SelectedNodes[0],
                            out var prefabRef)) {
                        return true;
                    }

                    prefab = prefabRef.m_Prefab;
                }

                return CanTunnel(prefab);
            }
        }

        // ── Parameters

        public NetPrefabParameter         NetPrefab  = new("connect.netPrefab", nullable: true);
        public EnumParameter<ConnectMode> Mode       = new("connect.mode", ConnectMode.SimpleCurve, label: "NetworkTools.UI.Common.Mode");

        /// <summary>
        ///     Tunnel mode.
        ///     Off: the vanilla course solver stays at or above the line between the two nodes.
        ///     It follows the terrain above that line (bridges over dips).
        ///     On: the network is held on the curve whatever the ground does (tunnels under hills).
        ///     The curve is cut so that a tunnel starts where the whole width is deep enough.
        ///     See the definitions job.
        /// </summary>
        public BoolParameter Tunnel = new("connect.tunnel", false, label: "NetworkTools.UI.Common.Tunnel");

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

        // Inner control points (points 3 and 5 of the two-bezier chain). Free position handles that
        // replace the mid rotation handle. Each translates rigidly when the mid node moves (bare
        // anchor) and mirrors the other through the node when dragged (custom MirrorMidControlPoint),
        // so the two beziers always join smoothly while the player positions each side independently.
        public Float3Parameter ComplexMidStartControlPointPosition = new(
            "connect.complexMidStartControlPointPosition",
            modes: (int)ConnectMode.ComplexCurve) {
            Handles = new IHandleSpec<float3>[] {
                new PositionHandle {
                    Style              = HandleTypeFlags.BezierControlPoint,
                    Size               = NT_Dimensions.HANDLE_AXIS_CIRCLE_RADIUS,
                    RenderConnectionTo = nameof(ComplexMidPosition),
                    DependsOn          = new Dependency[] {
                        nameof(ComplexMidPosition),
                        new(nameof(ComplexMidEndControlPointPosition), MirrorMidControlPoint),
                    },
                }
            }
        };
        public Float3Parameter ComplexMidEndControlPointPosition = new(
            "connect.complexMidEndControlPointPosition",
            modes: (int)ConnectMode.ComplexCurve) {
            Handles = new IHandleSpec<float3>[] {
                new PositionHandle {
                    Style              = HandleTypeFlags.BezierControlPoint,
                    Size               = NT_Dimensions.HANDLE_AXIS_CIRCLE_RADIUS,
                    RenderConnectionTo = nameof(ComplexMidPosition),
                    DependsOn          = new Dependency[] {
                        nameof(ComplexMidPosition),
                        new(nameof(ComplexMidStartControlPointPosition), MirrorMidControlPoint),
                    },
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
                    // The handle slides the circle centre along the bisector: factor 0 is the
                    // tightest reachable arc, factor 1 the widest.
                    DependsOn = new Dependency[] {
                        nameof(StartPosition), nameof(EndPosition),
                        nameof(StartDirection), nameof(EndDirection),
                        // The bisector the handle slides along is also chosen by the shape, and its
                        // height by the ramp grade. Neither is a position, so they cannot ride the
                        // bare (Float3) re-resolve path -- they carry an explicit re-resolve instead.
                        // Without it the knob stays where the *previous* shape left it, which is what
                        // made switching 90° <-> 270° look like the centre never moved.
                        new Dependency(nameof(LoopShapeMode), OnLoopShapeChanged),
                        new Dependency(nameof(LoopGrade), ResyncLoopHandle),
                        new Dependency(nameof(LoopHeight), ResyncLoopHandle),
                    },
                    StartPoint = tool => {
                        LoopGenerator.TryGetCenterAxis((NT_ConnectToolSystem)tool, out var from, out _);
                        return from;
                    },
                    EndPoint = tool => {
                        LoopGenerator.TryGetCenterAxis((NT_ConnectToolSystem)tool, out _, out var to);
                        return to;
                    },
                    YOffset = 1f,
                }
            }
        };
        /// <summary>
        ///     Shape of the loop. On <b>intersecting</b> roads: orthogonal (arc cuts the corner, 90° on a
        ///     right-angle junction) or crossing (arc goes the long way round, 270°). On <b>parallel</b>
        ///     roads: u-turn (one 180° half circle between two opposite-way roads) or stacked (two 270°
        ///     loops joined by an elevated deck between two same-way roads). The old per-quadrant
        ///     selector is gone: which branch you get is fully determined by which side of the corner
        ///     the centre sits on, i.e. by whether the sweep is short or long.
        /// </summary>
        public EnumParameter<LoopShape> LoopShapeMode = new("connect.loopShape", LoopShape.Orthogonal,
            modes: (int)ConnectMode.Loop,
            label: "NetworkTools.UI.Connect.LoopShape");

        /// <summary>
        ///     Design grade of the whole connection, as a fraction (0.04 = 4%). It sets how much of
        ///     the link is spent climbing: <c>|Δh| / grade</c> metres rise at exactly this pitch and
        ///     the leftover length stays level, centred on the loop. When that climb does not fit in
        ///     the link at all, the whole link rises at <c>|Δh| / length</c> instead -- the nodes are
        ///     pinned, so the profile has to close.
        /// </summary>
        public FloatParameter LoopGrade = new("connect.loopGrade", 0.04f, 0f, 0.15f,
            modes: (int)ConnectMode.Loop,
            label: "NetworkTools.UI.Connect.LoopGrade",
            fractionDigits: 1,
            numberType: NumberType.Percentage,
            displayScale: 100f);

        /// <summary>
        ///     Height of the flyover deck, in metres. <b>Only the stacked shape has a deck</b>;
        ///     the field is hidden for the other shapes, whose profile is fully determined by the two
        ///     nodes and the design grade.
        ///     <para>
        ///         Positive lifts the deck (viaduct), negative drops it (underpass / tunnel). The
        ///         reference is the start of the first loop, so the value is exactly how much each
        ///         ramp climbs between its node and the deck. The plan geometry, the radius and both
        ///         node positions are untouched.
        ///     </para>
        /// </summary>
        public FloatParameter LoopHeight = new("connect.loopHeight", 0f, -30f, 30f,
            modes: (int)ConnectMode.Loop,
            label: "NetworkTools.UI.Connect.LoopHeight",
            fractionDigits: 1,
            numberType: NumberType.Distance,
            displayScale: 1f);

        /// <summary>
        ///     Custom dependency reaction shared by the two inner control points. When the sibling
        ///     <paramref name="source" /> is dragged, this point is placed on the opposite ray through
        ///     the mid node so the two beziers stay tangent-continuous (G1). It keeps its own arm
        ///     length — only its direction flips — so each side is positioned independently. Computed
        ///     in the XZ plane and preserving this point's own height, so a horizontal drag of one
        ///     handle never drags the other vertically.
        /// </summary>
        private static void MirrorMidControlPoint(NT_BaseToolSystem tool, ParameterBase owner, ParameterBase source) {
            var ct  = (NT_ConnectToolSystem)tool;
            var mid = ct.ComplexMidPosition.Value;
            var own = (Float3Parameter)owner;

            var toSibling = ((Float3Parameter)source).Value - mid;
            toSibling.y = 0f;
            if (math.lengthsq(toSibling) < 1e-6f) return; // sibling over the node: axis undefined

            var axis     = math.normalize(toSibling);
            var armLen   = math.distance(own.Value.xz, mid.xz); // preserve this point's own distance
            var mirrored = mid - axis * armLen;
            mirrored.y   = own.Value.y;                          // keep our own height
            own.SetValue(mirrored, ChangeOrigin.Dependency);
        }

        /// <summary>
        ///     Custom dependency reaction for the loop radius knob. The knob's position is not a value
        ///     the tool stores -- it is recomputed from the current geometry each time, because the
        ///     bisector it slides along is chosen by the shape and its height by the ramp grade. A
        ///     change to either of those re-resolves the handle so the knob lands on the new axis
        ///     instead of staying where the previous geometry put it.
        /// </summary>
        private static void ResyncLoopHandle(NT_BaseToolSystem tool, ParameterBase owner, ParameterBase source) {
            tool.SyncHandlesFor(owner);
        }

        /// <summary>
        ///     Shape switch: re-resolve the knob, and give stacked a usable deck height the first time it
        ///     is picked. With a zero offset the two loops would run dead flat between the nodes --
        ///     technically buildable, but not what "stacked" means, and the negative-capable slider
        ///     makes zero an easy place to sit by accident.
        /// </summary>
        private static void OnLoopShapeChanged(NT_BaseToolSystem tool, ParameterBase owner, ParameterBase source) {
            var connect = (NT_ConnectToolSystem)tool;

            if (connect.LoopShapeMode.Value == LoopShape.Stacked
                && connect.LoopHeight.Value > -0.05f
                && connect.LoopHeight.Value < 0.05f) {
                connect.LoopHeight.SetValue(10f, ChangeOrigin.Dependency);
            }

            connect.SyncHandlesFor(owner);
        }

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
