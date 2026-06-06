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

            // ── Right-click pops a control point in any phase ─────────────────
            if (m_SecondaryApplyAction.WasPressedThisFrame()) {
                PopControlPoint();
                return HandleOutput(inputDeps);
            }

            // ── Handle interactions (Ready only) ─────────────────────────────
            if (Phase == OperationPhase.Ready && ProcessHandleInput(inputDeps)) {
                return HandleOutput(inputDeps);
            }

            // ── Precise rotation (Idle only) ─────────────────────────────────
            if (Phase == OperationPhase.Idle && m_PreciseRotation.IsInProgress()) {
                ApplyPreciseRotation();
            }

            // ── Raycast + input (Idle and Configuring) ───────────────────────
            if (Phase != OperationPhase.Ready) {
                var controlPoint = Raycast(inputDeps);
                if (controlPoint.HasValue) {
                    if (m_ApplyAction.WasPressedThisFrame())
                        PushControlPoint(controlPoint.Value);
                    else
                        UpdateHover(controlPoint.Value);
                }
            }

            // ── Output ───────────────────────────────────────────────────────
            return HandleOutput(inputDeps);
        }

        /// <summary>
        ///     Performs a raycast and applies snap if enabled. Returns null if no hit.
        /// </summary>
        private ControlPoint? Raycast(JobHandle inputDeps) {
            if (!GetRaycastResult(out var controlPoint))
                return null;

            if (SelectedSnaps != SnapOption.None && NetPrefab.NetPrefabEntity != Unity.Entities.Entity.Null) {
                TrySnapWorld(controlPoint, SelectedSnaps, GetSnapPrefab(), GetSnapElevation(), inputDeps,
                             out var snapped, out var won);
                if (won) controlPoint = snapped;
            }

            return controlPoint;
        }

        /// <summary>
        ///     Adds a permanent control point. Transitions phase forward.
        /// </summary>
        private void PushControlPoint(ControlPoint cp) {
            m_ControlPoints.Add(cp);
            Phase = DerivePhase();
            SyncFromControlPoints();

            if (Phase == OperationPhase.Ready)
                RebuildHandlesForActiveMode();

            m_UpdateNeeded = true;
        }

        /// <summary>
        ///     Removes the last control point. Transitions phase backward.
        ///     If no control points remain, exits the tool.
        /// </summary>
        private void PopControlPoint() {
            if (m_ControlPoints.Length == 0) {
                RequestDisable();
                return;
            }

            if (Phase == OperationPhase.Ready)
                DestroyAllHandles();

            m_ControlPoints.Length--;
            Phase = DerivePhase();
            SyncFromControlPoints();
            m_UpdateNeeded = true;
        }

        /// <summary>
        ///     Updates transient hover state. In Idle, sets origin position.
        ///     In Configuring, derives mode-specific parameters from the cursor.
        /// </summary>
        private void UpdateHover(ControlPoint cp) {
            switch (Phase) {
                case OperationPhase.Idle:
                    Position.Value      = cp.m_Position;
                    m_BaselineElevation = cp.m_Elevation;
                    m_UpdateNeeded      = true;
                    break;
                case OperationPhase.Configuring:
                    InitializeFromSecondPoint(cp.m_Position);
                    m_UpdateNeeded = true;
                    break;
            }
        }

        private void InitializeFromSecondPoint(float3 secondPos) {
            switch (Mode.Value) {
                case GenerateMode.Grid:   GridGenerator.Initialize(this, secondPos);   break;
                case GenerateMode.Circle: CircleGenerator.Initialize(this, secondPos); break;
                case GenerateMode.Oval:   OvalGenerator.Initialize(this, secondPos);   break;
            }

            GridDirectionPoint.Value = secondPos;
            OvalAxisPoint.Value      = secondPos;
        }

        /// <summary>
        ///     Syncs all parameters from the current control point state.
        ///     Called after push/pop.
        /// </summary>
        private void SyncFromControlPoints() {
            if (m_ControlPoints.Length == 0) return;

            Position.Value      = m_ControlPoints[0].m_Position;
            m_BaselineElevation = m_ControlPoints[0].m_Elevation;

            if (m_ControlPoints.Length >= 2)
                InitializeFromSecondPoint(m_ControlPoints[1].m_Position);
        }

        private void ApplyPreciseRotation() {
            var input = m_PreciseRotation.ReadValue<float>();
            var delta = math.PI * 0.5f * input * UnityEngine.Time.deltaTime;
            var current = Rotation.Value;
            var angle = math.atan2(current.x, current.z) + delta;
            Rotation.Value = new float3(math.sin(angle), 0, math.cos(angle));
            m_UpdateNeeded = true;
        }

        /// <inheritdoc />
        public int ApplyMinNodeCount => 0;

        /// <inheritdoc />
        public bool CanApply => Phase == OperationPhase.Ready;

        /// <summary>
        ///     Requests the tool to apply the current network.
        /// </summary>
        public void RequestApply() {
            if (Phase != OperationPhase.Ready)
                return;

            Phase = OperationPhase.Applying;
        }

        /// <summary>
        ///     Resets the tool to idle state, clearing all control points and handles.
        /// </summary>
        public void ResetToIdle() {
            Phase = OperationPhase.Idle;
            DestroyAllHandles();
            m_ControlPoints.Clear();
            ClearAllHighlights();
        }

        /// <summary>
        ///     Dispatches output based on current phase.
        /// </summary>
        private JobHandle HandleOutput(JobHandle inputDeps) {
            return Phase switch {
                OperationPhase.Applying => Apply(inputDeps),
                _                       => Update(inputDeps)
            };
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

            m_ToolRaycastSystem.typeMask    = TypeMask.Terrain | TypeMask.Net;
            m_ToolRaycastSystem.raycastFlags = RaycastFlags.Markers | RaycastFlags.ElevateOffset;
        }
    }
}
