namespace NetworkTools.Systems.Tools.Generate {
    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Tools;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    ///     Update loop and state management for <see cref="NT_GenerateToolSystem"/>.
    /// </summary>
    public partial class NT_GenerateToolSystem {
        /// <inheritdoc />
        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            UpdateActions();

            // ═══════════════════════════════════════════════════════════════════════════
            // HANDLE INTERACTION PIPELINE
            // ═══════════════════════════════════════════════════════════════════════════

            if (Phase == OperationPhase.Ready && ProcessHandleInput()) {
                return HandleTempEntities(inputDeps);
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // CONTROL POINT PLACEMENT: Input Detection
            // ═══════════════════════════════════════════════════════════════════════════

            var          rightClickPressed       = m_SecondaryApplyAction.WasPressedThisFrame();
            var          leftClickPressed        = m_ApplyAction.WasPressedThisFrame();
            var          raycastHit              = false;
            var          hitPosition             = float3.zero;
            var          lastHoveredControlPoint = m_HoveredControlPoint.value;
            var          hasSelectedControlPoint = !m_SelectedControlPoint.value.Equals(default);
            var          hasNewHoverTarget       = false;
            ControlPoint controlPoint            = default;

            raycastHit = GetRaycastResult(out controlPoint);
            if (raycastHit) {
                hitPosition = controlPoint.m_HitPosition;
                hasNewHoverTarget = !controlPoint.Equals(lastHoveredControlPoint);
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // CONTROL POINT PLACEMENT: State Mutation
            // ═══════════════════════════════════════════════════════════════════════════

            if (rightClickPressed) {
                // todo handle rotation
                HandleRemoveControlPoint();
                m_UpdateNeeded = true;
            } else if (raycastHit && !hasSelectedControlPoint && leftClickPressed) {
                HandleAddControlPoint(controlPoint);
                m_UpdateNeeded = true;
            } else if (raycastHit && !hasSelectedControlPoint && hasNewHoverTarget) {
                m_HoveredControlPoint.value = controlPoint;
                m_UpdateNeeded = true;
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // OUTPUT
            // ═══════════════════════════════════════════════════════════════════════════

            return HandleTempEntities(inputDeps);
        }

        /// <summary>
        ///     Adds a control point at the given position. Transitions phase accordingly.
        /// </summary>
        /// <returns>True if the control point was added.</returns>
        protected bool HandleAddControlPoint(ControlPoint controlPoint) {
            if (m_ControlPoints.Length != 0) {
                return false;
            }

            m_Log.Debug($"Adding control point at {controlPoint.m_Position}");

            m_ControlPoints.Add(controlPoint);
            UpdatePhaseFromSelection();

            // When a point is placed, initialize the config
            if (Phase == OperationPhase.Ready)
            {
                InitializeConfig();
            }

            return true;
        }

        /// <summary>
        ///     Removes the last control point. Transitions phase accordingly.
        /// </summary>
        /// <returns>True if a control point was removed.</returns>
        protected bool HandleRemoveControlPoint() {
            if (m_ControlPoints.Length == 0) {
                m_Log.Debug("Cancel pressed, exiting tool.");
                RequestDisable();
                return false;
            }

            m_Log.Debug($"Removing last control point");

            m_ControlPoints.RemoveAt(0);
            UpdatePhaseFromSelection();

            // Destroy handles when moving out of Ready
            if (Phase != OperationPhase.Ready)
            {
                DestroyAllHandles();
            }

            return true;
        }

        /// <summary>
        ///     Updates the OperationPhase based on the number of control points.
        /// </summary>
        /// <returns>The previous phase before the update.</returns>
        private OperationPhase UpdatePhaseFromSelection() {
            var previousPhase = Phase;

            Phase = m_ControlPoints.Length switch {
                0 => OperationPhase.Idle,
                1 => OperationPhase.Ready,
            };

            return previousPhase;
        }

        /// <summary>
        ///     Dispatches temp entity handling based on current phase.
        ///     For Generate, we use:
        ///     - OperationPhase.Idle: No control point, player will see hover preview
        ///     - OperationPhase.Ready: Control point was set, player can use handles +  UI to configure
        ///     - OperationPhase.Applying: Player requested apply, show final preview and apply 
        /// </summary>
        private JobHandle HandleTempEntities(JobHandle inputDeps) {
            return Phase switch {
                OperationPhase.Idle or OperationPhase.Ready => Update(inputDeps),
                OperationPhase.Applying                     => Apply(inputDeps),
                _                                           => Clear(inputDeps)
            };
        }

        /// <summary>
        ///     Requests the tool to apply the current grid.
        /// </summary>
        public void RequestApply() {
            if (Phase != OperationPhase.Ready) {
                return;
            }

            Phase = OperationPhase.Applying;
        }

        /// <summary>
        ///     Resets the tool to idle state, clearing all control points and handles.
        /// </summary>
        public void ResetToIdle() {
            Phase = OperationPhase.Idle;
            DestroyAllHandles();
            ClearSelectionState();
        }

        /// <summary>
        ///     Clears all control point state.
        /// </summary>
        protected void ClearSelectionState() {
            m_ControlPoints.Clear();
            ClearAllHighlights();
        }

        /// <inheritdoc />
        protected override bool GetRaycastResult(out ControlPoint controlPoint) {
            if (base.GetRaycastResult(out var entity, out RaycastHit raycastHit)) {
                controlPoint = new ControlPoint(entity, raycastHit);
                return true;
            }

            controlPoint = default;
            return false;
        }

        /// <inheritdoc />
        public override void InitializeRaycast() {
            base.InitializeRaycast();

            m_ToolRaycastSystem.collisionMask =
                CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.Underground;
            m_ToolRaycastSystem.typeMask       = TypeMask.Terrain | TypeMask.Net;
            m_ToolRaycastSystem.netLayerMask   = Layer.All;
            m_ToolRaycastSystem.iconLayerMask  = IconLayerMask.None;
            m_ToolRaycastSystem.utilityTypeMask = UtilityTypes.None;
            m_ToolRaycastSystem.raycastFlags    = RaycastFlags.Markers | RaycastFlags.ElevateOffset |
                                                  RaycastFlags.SubElements |
                                                  RaycastFlags.Cargo | RaycastFlags.Passenger;
        }
    }
}
