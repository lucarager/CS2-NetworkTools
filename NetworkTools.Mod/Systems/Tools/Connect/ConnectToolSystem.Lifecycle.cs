namespace NetworkTools.Systems.Tools.Connect {
    using Colossal.Entities;
    using Colossal.Mathematics;

    using Game.Net;
    using Game.Prefabs;
    using NetworkTools.Components.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Lifecycle and config initialization for <see cref="NT_ConnectToolSystem"/>.
    /// </summary>
    public partial class NT_ConnectToolSystem {
        /// <inheritdoc />
        public bool HasToolComponent(PrefabBase prefab) {
            return m_PrefabSystem.HasComponent<NT_ConnectTool>(prefab);
        }

        private void InitializeParameters() {
            m_Log.Debug($"InitializeParameters: Initializing {Mode.Value}");

            if (Phase != OperationPhase.Ready) {
                return;
            }

            var startNodeEntity     = m_SelectedNodes[0];
            var endNodeEntity       = m_SelectedNodes[1];
            var startNode           = EntityManager.GetComponentData<Node>(startNodeEntity);
            var endNode             = EntityManager.GetComponentData<Node>(endNodeEntity);
            var startConnectedEdges = EntityManager.GetBuffer<ConnectedEdge>(startNodeEntity);
            var endConnectedEdges   = EntityManager.GetBuffer<ConnectedEdge>(endNodeEntity);
            var startPosition       = startNode.m_Position;
            var endPosition         = endNode.m_Position;

            StartPosition.Value  = startPosition;
            EndPosition.Value    = endPosition;
            StartDirection.Value = ComputeNodeDirection(startNodeEntity, startConnectedEdges, startPosition, endPosition);
            EndDirection.Value   = ComputeNodeDirection(endNodeEntity, endConnectedEdges, endPosition, startPosition);

            switch (Mode.Value) {
                case ConnectMode.SimpleCurve:  SimpleCurveGenerator.Initialize(this);  break;
                case ConnectMode.ComplexCurve: ComplexCurveGenerator.Initialize(this); break;
                case ConnectMode.Loop:         LoopGenerator.Initialize(this);         break;
            }

            RebuildHandlesForActiveMode();
        }

        /// <summary>
        /// Computes the outgoing direction for a selected node based on its connectivity:
        /// <list type="bullet">
        ///   <item>End node (1 edge): continues the direction of the connected edge.</item>
        ///   <item>In-between node (2 edges): perpendicular to the through-direction, facing the other node.</item>
        ///   <item>Intersection (3+ edges) or isolated: horizontal vector toward the other node.</item>
        /// </list>
        /// </summary>
        /// <param name="nodeEntity">The node entity to compute the direction for.</param>
        /// <param name="connectedEdges">The connected edges buffer for the node.</param>
        /// <param name="nodePosition">Position of this node.</param>
        /// <param name="otherPosition">Position of the other selected node (used for fallback and perpendicular orientation).</param>
        private float3 ComputeNodeDirection(
            Entity nodeEntity,
            DynamicBuffer<ConnectedEdge> connectedEdges,
            float3 nodePosition,
            float3 otherPosition) {
            var edgeCount = connectedEdges.Length;

            if (edgeCount == 1) {
                // End node: continue the direction of the connected edge past the dead end
                return ComputeEndNodeDirection(nodeEntity, connectedEdges[0].m_Edge, nodePosition, otherPosition);
            }

            if (edgeCount == 2) {
                // In-between node: perpendicular to the road's through-direction
                return ComputeInBetweenNodeDirection(nodeEntity, connectedEdges[0].m_Edge, nodePosition, otherPosition);
            }

            // Intersection (3+ edges) or isolated (0 edges): direct vector toward the other node
            return ComputeDirectionToward(nodePosition, otherPosition);
        }

        /// <summary>
        /// For an end node, returns the continuation direction past the dead end (away from the connected edge).
        /// Preserves the full 3D tangent so the curve follows the road's slope.
        /// </summary>
        private float3 ComputeEndNodeDirection(Entity nodeEntity, Entity edgeEntity, float3 nodePosition, float3 otherPosition) {
            if (!EntityManager.TryGetComponent<Edge>(edgeEntity, out var edge) ||
                !EntityManager.TryGetComponent<Curve>(edgeEntity, out var curve)) {
                return ComputeDirectionToward(nodePosition, otherPosition);
            }

            // Get the tangent at this node that continues the road direction past the dead end:
            // - If this node is the edge's end:   the road arrives going in EndTangent direction → continue that way.
            // - If this node is the edge's start:  the road departs in StartTangent direction  → continue opposite.
            var tangent = edge.m_End == nodeEntity
                ? MathUtils.EndTangent(curve.m_Bezier)
                : -MathUtils.StartTangent(curve.m_Bezier);

            return math.normalizesafe(tangent, ComputeDirectionToward(nodePosition, otherPosition));
        }

        /// <summary>
        /// For an in-between node (2 edges), returns a direction perpendicular to the road's
        /// through-direction, oriented toward the other selected node.
        /// Operates in full 3D so the result follows the road's slope.
        /// </summary>
        private float3 ComputeInBetweenNodeDirection(Entity nodeEntity, Entity edgeEntity, float3 nodePosition, float3 otherPosition) {
            if (!EntityManager.TryGetComponent<Edge>(edgeEntity, out var edge) ||
                !EntityManager.TryGetComponent<Curve>(edgeEntity, out var curve)) {
                return ComputeDirectionToward(nodePosition, otherPosition);
            }

            var throughDir = edge.m_Start == nodeEntity
                ? MathUtils.StartTangent(curve.m_Bezier)
                : -MathUtils.EndTangent(curve.m_Bezier);
            throughDir = math.normalizesafe(throughDir, new float3(1, 0, 0));

            // Project toOther onto the plane perpendicular to throughDir
            var toOther = otherPosition - nodePosition;
            var perpendicular = toOther - throughDir * math.dot(toOther, throughDir);

            if (math.lengthsq(perpendicular) < 0.0001f) {
                perpendicular = math.cross(throughDir, new float3(0, 1, 0));
                if (math.lengthsq(perpendicular) < 0.0001f) {
                    perpendicular = math.cross(throughDir, new float3(1, 0, 0));
                }
            }

            return math.normalizesafe(perpendicular);
        }

        /// <summary>
        /// Returns the 3D direction from one position to another.
        /// </summary>
        private static float3 ComputeDirectionToward(float3 from, float3 to) {
            return math.normalizesafe(to - from, new float3(1, 0, 0));
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_Log.Prefix = nameof(NT_ConnectToolSystem);

            // Configuration
            RenderHandles             = true;
            //DisableVanillaCourseSplit = true;

            // Mode change reinitializes context and handles
            Mode.OnChanged += _ => {
                if (Phase == OperationPhase.Ready)
                    InitializeParameters();
            };

            // Data
            m_SelectedNodes = new NativeList<Entity>(32, Allocator.Persistent);
        }

        protected override void OnDestroy() {
            if (m_SelectedNodes.IsCreated) {
                m_SelectedNodes.Dispose();
            }

            base.OnDestroy();
        }

        protected override void OnStartRunning() {
            base.OnStartRunning();

            // Reset internal state
            m_LastHitPosition = default;
            Phase             = OperationPhase.Idle;
            base.requireNetArrows = true;

            // Initialize selection state (makes all nodes eligible)
            ResetToIdle();
        }

        protected override void OnStopRunning() {
            base.OnStopRunning();

            base.requireNetArrows = false;

            // Clear selection state
            ClearSelectionState();
        }
    }
}
