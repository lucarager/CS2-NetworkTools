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

        /// <summary>
        ///     Calls InitializeConfig on the current generator to compute initial values.
        /// </summary>
        private void InitializeConfig() {
            m_Log.Debug($"InitializeConfig: Initializing {Mode.Value}");

            // Only initialize config when we have 2 valid nodes selected (Ready phase)
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

            var startDirection = ComputeNodeDirection(startNodeEntity, startConnectedEdges, startPosition, endPosition);
            var endDirection   = ComputeNodeDirection(endNodeEntity, endConnectedEdges, endPosition, startPosition);

            var config = new ConnectJobConfig {
                StartPosition  = startPosition,
                EndPosition    = endPosition,
                StartDirection = startDirection,
                EndDirection   = endDirection,
            };

            switch (Mode.Value) {
                case ConnectMode.SimpleCurve:
                    new SimpleCurveGenerator().InitializeConfig(ref config);
                    break;
                case ConnectMode.Loop:
                    new LoopGenerator().InitializeConfig(ref config);
                    break;
            }

            // Copy snapshot back to parameters
            StartPosition.Value                  = config.StartPosition;
            EndPosition.Value                    = config.EndPosition;
            StartDirection.Value                 = config.StartDirection;
            EndDirection.Value                   = config.EndDirection;
            CurveStartPointPosition.Value        = config.CurveStartPointPosition;
            CurveStartControlPointPosition.Value = config.CurveStartControlPointPosition;
            CurveEndControlPointPosition.Value   = config.CurveEndControlPointPosition;
            CurveEndPointPosition.Value          = config.CurveEndPointPosition;
            LoopControlPointPosition.Value       = config.LoopControlPointPosition;
            LoopRadius.Value                     = config.LoopRadius;

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

            // Intersection (3+ edges) or isolated (0 edges): horizontal vector toward the other node
            return ComputeHorizontalDirection(nodePosition, otherPosition);
        }

        /// <summary>
        /// For an end node, returns the continuation direction past the dead end (away from the connected edge).
        /// </summary>
        private float3 ComputeEndNodeDirection(Entity nodeEntity, Entity edgeEntity, float3 nodePosition, float3 otherPosition) {
            if (!EntityManager.TryGetComponent<Edge>(edgeEntity, out var edge) ||
                !EntityManager.TryGetComponent<Curve>(edgeEntity, out var curve)) {
                return ComputeHorizontalDirection(nodePosition, otherPosition);
            }

            // Get the tangent at this node that continues the road direction past the dead end:
            // - If this node is the edge's end:   the road arrives going in EndTangent direction → continue that way.
            // - If this node is the edge's start:  the road departs in StartTangent direction  → continue opposite.
            var tangent = edge.m_End == nodeEntity
                ? MathUtils.EndTangent(curve.m_Bezier)
                : -MathUtils.StartTangent(curve.m_Bezier);

            tangent.y = 0f;
            return math.normalizesafe(tangent, ComputeHorizontalDirection(nodePosition, otherPosition));
        }

        /// <summary>
        /// For an in-between node (2 edges), returns a direction perpendicular to the road's
        /// through-direction, oriented toward the other selected node.
        /// </summary>
        private float3 ComputeInBetweenNodeDirection(Entity nodeEntity, Entity edgeEntity, float3 nodePosition, float3 otherPosition) {
            if (!EntityManager.TryGetComponent<Edge>(edgeEntity, out var edge) ||
                !EntityManager.TryGetComponent<Curve>(edgeEntity, out var curve)) {
                return ComputeHorizontalDirection(nodePosition, otherPosition);
            }

            // Through-direction: the tangent leaving this node along the first edge
            var throughDir = edge.m_Start == nodeEntity
                ? MathUtils.StartTangent(curve.m_Bezier)
                : -MathUtils.EndTangent(curve.m_Bezier);

            throughDir.y = 0f;
            throughDir = math.normalizesafe(throughDir, new float3(1, 0, 0));

            // Perpendicular in the XZ plane
            var perpendicular = new float3(throughDir.z, 0f, -throughDir.x);

            // Orient toward the other selected node
            var toOther = otherPosition - nodePosition;
            toOther.y = 0f;
            if (math.dot(perpendicular, toOther) < 0f) {
                perpendicular = -perpendicular;
            }

            return perpendicular;
        }

        /// <summary>
        /// Returns the horizontal direction from one position to another, flattened to XZ.
        /// </summary>
        private static float3 ComputeHorizontalDirection(float3 from, float3 to) {
            var delta = to - from;
            delta.y = 0f;
            return math.normalizesafe(delta, new float3(1, 0, 0));
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_Log.Prefix = nameof(NT_ConnectToolSystem);

            // Configuration
            RenderEligibleNodes      = true;
            RenderHandles            = true;
            DisableVanillaValidation = true;

            // Mode change reinitializes context and handles
            Mode.OnChanged += _ => {
                if (Phase == OperationPhase.Ready)
                    InitializeConfig();
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

            // Initialize selection state (makes all nodes eligible)
            ResetToIdle();
        }

        protected override void OnStopRunning() {
            base.OnStopRunning();

            // Clear selection state
            ClearSelectionState();
        }
    }
}
