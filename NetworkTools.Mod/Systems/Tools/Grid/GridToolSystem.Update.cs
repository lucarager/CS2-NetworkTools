namespace NetworkTools.Systems.Tools {
    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using Game.Notifications;

    using NetworkTools.Components;

    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    using static Colossal.IO.AssetDatabase.AtlasFrame;

    /// <summary>
    ///     Update loop and state management for <see cref="NT_GridToolSystem"/>.
    /// </summary>
    public partial class NT_GridToolSystem {
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

            var rightClickPressed = m_SecondaryApplyAction.WasPressedThisFrame();
            var leftClickPressed  = m_ApplyAction.WasPressedThisFrame();
            var raycastHit        = false;
            var hitPosition       = float3.zero;
            ControlPoint controlPoint = default;

            raycastHit = GetRaycastResult(out controlPoint);
            if (raycastHit) {
                hitPosition = controlPoint.m_HitPosition;
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // CONTROL POINT PLACEMENT: State Mutation
            // ═══════════════════════════════════════════════════════════════════════════

            if (rightClickPressed) {
                HandleRemoveControlPoint();
                m_UpdateNeeded = true;
            } else if (raycastHit && leftClickPressed) {
                HandleAddControlPoint(hitPosition);
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
        /// <param name="position">World position for the new control point.</param>
        /// <returns>True if the control point was added.</returns>
        protected bool HandleAddControlPoint(float3 position) {
            if (CurrentSelectionState == SelectionState.EndNodeSelected) {
                return false;
            }

            m_Log.Debug($"[{CurrentSelectionState}] Adding control point at {position}");

            m_ControlPoints.Add(position);
            UpdatePhaseFromSelection();

            // When second point is placed, initialize the grid config
            if (Phase == OperationPhase.Ready) {
                InitializeConfig();
            }

            return true;
        }

        /// <summary>
        ///     Removes the last control point. Transitions phase accordingly.
        /// </summary>
        /// <returns>True if a control point was removed.</returns>
        protected bool HandleRemoveControlPoint() {
            if (CurrentSelectionState == SelectionState.NoSelection) {
                m_Log.Debug("Cancel pressed, exiting tool.");
                RequestDisable();
                return false;
            }

            m_Log.Debug($"[{CurrentSelectionState}] Removing last control point");

            m_ControlPoints.RemoveAt(m_ControlPoints.Length - 1);
            UpdatePhaseFromSelection();

            // Destroy handles when moving out of Ready
            if (Phase != OperationPhase.Ready) {
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

            Phase = CurrentSelectionState switch {
                SelectionState.NoSelection        => OperationPhase.Idle,
                SelectionState.StartNodeSelected  => OperationPhase.Configuring,
                _                                 => OperationPhase.Ready
            };

            return previousPhase;
        }

        /// <summary>
        ///     Dispatches temp entity handling based on current phase.
        /// </summary>
        private JobHandle HandleTempEntities(JobHandle inputDeps) {
            return Phase switch {
                OperationPhase.Ready                              => Update(inputDeps),
                OperationPhase.Applying                           => Apply(inputDeps),
                OperationPhase.Idle or OperationPhase.Configuring => Clear(inputDeps),
                _                                                 => Clear(inputDeps)
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
