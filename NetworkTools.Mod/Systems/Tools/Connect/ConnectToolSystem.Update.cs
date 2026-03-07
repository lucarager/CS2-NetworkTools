namespace NetworkTools.Systems.Tools.Connect {
    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.Tools;

    using NetworkTools.Components;
    using NetworkTools.Components.Tools;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.RoadShape;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    using static Colossal.IO.AssetDatabase.AtlasFrame;

    /// <summary>
    ///     Tool system for
    /// </summary>
    public partial class NT_ConnectToolSystem {
        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            UpdateActions();

            // ═══════════════════════════════════════════════════════════════════════════
            // HANDLE INTERACTION PIPELINE 
            // ═══════════════════════════════════════════════════════════════════════════

            if (Phase == OperationPhase.Ready && ProcessHandleInput())
            {
                // Handle consumed input this frame:
                // - OnHandleDragging() may have updated ShapeTransformConfig
                // - m_UpdateNeeded was set to true
                // - Skip node selection, go straight to output
                return HandleTempEntities(inputDeps);
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // NODE SELECTION: Input Detection 
            // ═══════════════════════════════════════════════════════════════════════════

            var rightClickPressed = m_SecondaryApplyAction.WasPressedThisFrame();
            var leftClickPressed = m_ApplyAction.WasPressedThisFrame();
            var raycastHit = false;
            var hoveredEntity = Entity.Null;
            var hitPosition = float3.zero;
            ControlPoint controlPoint = default;

            raycastHit = GetRaycastResult(out controlPoint);
            if (raycastHit)
            {
                hoveredEntity = controlPoint.m_OriginalEntity;
                hitPosition = controlPoint.m_HitPosition;
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // NODE SELECTION: State Mutation
            // Phase is automatically updated by HandleAddNode/HandleRemoveNode
            // ═══════════════════════════════════════════════════════════════════════════

            // Right-click: cancel/back (skips all raycast processing)
            if (rightClickPressed)
            {
                HandleRemoveNode();
                m_UpdateNeeded = true;
            }
            // Raycast-based interactions
            else if (raycastHit)
            {
                // Update hover state first (so path preview is ready if user clicks)
                var newEntityHovered = (hoveredEntity != m_LastHoveredEntity.Value);
                if (newEntityHovered)
                {
                    HandleHover(hoveredEntity);
                    m_UpdateNeeded = true;
                }
                m_LastHoveredEntity.Value = hoveredEntity;
                m_LastHitPosition = hitPosition;

                // Left-click: add node (after hover update, same frame OK)
                if (leftClickPressed && hoveredEntity != Entity.Null)
                {
                    HandleAddNode(hoveredEntity);
                    m_UpdateNeeded = true;
                }
            }
            // No raycast hit
            else
            {
                HandleNoHover();
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // OUTPUT
            // ═══════════════════════════════════════════════════════════════════════════

            return HandleTempEntities(inputDeps);
        }


        /// <summary>
        ///     Updates highlighting based on new hover state.
        /// </summary>
        /// <param name="hoveredEntity">The entity being hovered.</param>
        protected void HandleHover(Entity hoveredEntity) {
            SwapHighlitedEntities(m_LastHoveredEntity.Value, hoveredEntity, NT_Highlighted.DefaultNode);
        }

        /// <summary>
        ///     Handles the case when hovering over nothing (no raycast hit).
        ///     Clears preview state and highlights.
        /// </summary>
        protected void HandleNoHover() {
            m_LastHoveredEntity.Value = Entity.Null;
            ClearAllHighlights();
        }

        /// <summary>
        ///     Removes the last node from the path. Returns true if selection changed.
        ///     Automatically updates Phase and invokes appropriate template methods.
        /// </summary>
        /// <returns>True if a node was removed and selection changed.</returns>
        protected bool HandleRemoveNode() {
            if (CurrentSelectionState == SelectionState.NoSelection)
            {
                m_Log.Debug("Cancel pressed, exiting tool.");
                RequestDisable();
                return false;
            }

            var lastNode = m_SelectedNodes[^1];

            m_Log.Debug($"[{CurrentSelectionState}] Removing node: {lastNode}");

            EntityManager.RemoveComponent<NT_Selected>(lastNode);
            EntityManager.RemoveComponent<NT_SelectedFirst>(lastNode);
            EntityManager.RemoveComponent<NT_SelectedLast>(lastNode);
            m_SelectedNodes.RemoveAt(m_SelectedNodes.Length - 1);

            // Update phase 
            UpdatePhaseFromSelection();

            // Recalculate eligible nodes
            MarkEligibleNodes();

            return true;
        }

        /// <summary>
        /// Adds node to selection.
        /// </summary>
        /// <param name="entity">The entity to add.</param>
        /// <returns>True if the node was added, false otherwise.</returns>
        protected bool HandleAddNode(Entity entity) {
            if (entity == Entity.Null || m_SelectedNodes.Contains(entity) || CurrentSelectionState == SelectionState.EndNodeSelected)
            {
                return false;
            }

            var previousState = CurrentSelectionState;

            m_Log.Debug($"[{CurrentSelectionState}] Adding node: {entity}");

            m_SelectedNodes.Add(entity);
            EntityManager.AddComponentData(entity, NT_Selected.DefaultNode);

            // Add node to selection and mark with state-specific components
            if (previousState == SelectionState.NoSelection) {
                EntityManager.AddComponent<NT_SelectedFirst>(entity);
            } else if (previousState == SelectionState.StartNodeSelected) {
                EntityManager.AddComponent<NT_SelectedLast>(entity);
            }

            // Update phase 
            UpdatePhaseFromSelection();

            // Recalculate eligible nodes
            MarkEligibleNodes();

            // Re-initialize config
            InitializeConfig();

            return true;
        }

        /// <summary>
        ///     Recalculates which nodes are eligible for selection based on current state
        /// </summary>
        private void MarkEligibleNodes() {
            if (CurrentSelectionState is SelectionState.NoSelection or SelectionState.StartNodeSelected) {
                // Clear all selected nodes
                foreach (var node in m_SelectedNodes) {
                    EntityManager.RemoveComponent<NT_Eligible>(node);
                }

                // Add to all eligible
                EntityManager.AddComponent<NT_Eligible>(m_UnselectedNodesWithoutEligibleQuery);
            } else if (CurrentSelectionState == SelectionState.EndNodeSelected) {
                EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);
            }
        }

        /// <summary>
        ///     Updates the OperationPhase based on the current selection state.
        ///     Called automatically by HandleAddNode/HandleRemoveNode.
        /// </summary>
        /// <returns>The previous phase before the update.</returns>
        private OperationPhase UpdatePhaseFromSelection() {
            var previousPhase = Phase;

            Phase = CurrentSelectionState switch {
                SelectionState.NoSelection => OperationPhase.Idle,
                SelectionState.StartNodeSelected => OperationPhase.Configuring,
                _ => OperationPhase.Ready
            };

            return previousPhase;
        }

        /// <summary>
        ///     Runs various jobs depending on whether we need to Update, Apply, or Cancel temp entities.
        /// </summary>
        /// <param name="inputDeps">Input job dependencies.</param>
        /// <returns>Output job handle.</returns>
        private JobHandle HandleTempEntities(JobHandle inputDeps) {
            return Phase switch {
                // Preview temp entities
                OperationPhase.Ready => Update(inputDeps),
                // Apply real entities
                OperationPhase.Applying => Apply(inputDeps),
                // Clear otherwise
                OperationPhase.Idle or OperationPhase.Configuring => Clear(inputDeps),
                _ => Clear(inputDeps)
            };
        }

        /// <summary>
        ///     Requests the tool to apply the current transformation.
        /// </summary>
        public void RequestApply() {
            if (Phase != OperationPhase.Ready)
            {
                return;
            }

            Phase = OperationPhase.Applying;
        }

        protected override bool GetRaycastResult(out ControlPoint controlPoint) {
            if (base.GetRaycastResult(out var entity, out RaycastHit raycastHit))
            {
                controlPoint = FilterRaycastResult(entity, raycastHit);
                return controlPoint.m_OriginalEntity != Entity.Null;
            }


            controlPoint = default;
            return false;
        }

        private ControlPoint FilterRaycastResult(Entity entity, RaycastHit hit) {
            var controlPoint = default(ControlPoint);
            var candidateEntity = Entity.Null;

            // If we hit an edge, find the closest node instead
            if (EntityManager.HasComponent<Edge>(entity))
            {
                // todo make job
                // Find the closest node to the hit position
                var edge = EntityManager.GetComponentData<Edge>(entity);
                var startNode = EntityManager.GetComponentData<Node>(edge.m_Start);
                var distanceToStart = math.distance(hit.m_Position, startNode.m_Position);
                var endNode = EntityManager.GetComponentData<Node>(edge.m_End);
                var distanceToEnd = math.distance(hit.m_Position, endNode.m_Position);

                if (distanceToStart < MaxDistanceToSelect && distanceToStart < distanceToEnd)
                {
                    candidateEntity = edge.m_Start;
                } else if (distanceToEnd < MaxDistanceToSelect && distanceToEnd < distanceToStart)
                {
                    candidateEntity = edge.m_End;
                }
            } else
            {
                candidateEntity = entity;
            }

            // Check that the entity we're hitting is eligible
            if (EntityManager.HasComponent<NT_Eligible>(candidateEntity))
            {
                controlPoint = new ControlPoint(candidateEntity, hit);
            }

            return controlPoint;
        }

        public override void InitializeRaycast() {
            base.InitializeRaycast();

            m_ToolRaycastSystem.collisionMask =
                CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.Underground;
            m_ToolRaycastSystem.typeMask = TypeMask.Net;
            m_ToolRaycastSystem.netLayerMask = Layer.All;
            m_ToolRaycastSystem.iconLayerMask = IconLayerMask.None;
            m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;
            m_ToolRaycastSystem.raycastFlags = RaycastFlags.Markers | RaycastFlags.ElevateOffset |
                                               RaycastFlags.SubElements |
                                               RaycastFlags.Cargo | RaycastFlags.Passenger;
        }

        /// <summary>
        ///     Resets the tool to idle state, clearing all selection.
        /// </summary>
        public void ResetToIdle() {
            // Clear state to completely blank
            Phase = OperationPhase.Idle;

            // Destroy any active handles
            DestroyAllHandles();

            // Use base class method to clear selection state
            ClearSelectionState();

            // Mark eligible nodes
            MarkEligibleNodes();
        }

        /// <summary>
        ///     Clears all selection state and resets to idle.
        ///     Called when tool stops or user explicitly resets.
        /// </summary>
        protected void ClearSelectionState() {
            // Clear caches
            m_SelectedNodes.Clear();

            // Batch remove all marker components using cached queries
            EntityManager.RemoveComponent<NT_Selected>(m_NodesWithSelectedQuery);
            EntityManager.RemoveComponent<NT_Selected>(m_EdgesWithSelectedQuery);
            EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);
            EntityManager.RemoveComponent<NT_SelectedFirst>(m_NodesWithSelectedFirstQuery);
            EntityManager.RemoveComponent<NT_SelectedLast>(m_NodesWithSelectedLastQuery);

            ClearAllHighlights();
        }
    }
}
