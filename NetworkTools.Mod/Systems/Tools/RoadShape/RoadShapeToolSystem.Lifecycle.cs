namespace NetworkTools.Systems.Tools.RoadShape {
    using System.Collections.Generic;

    using Game.Common;
    using Game.Input;
    using Game.Notifications;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;

    using NetworkTools.Components.Handles;
    using NetworkTools.Components;
    using NetworkTools.Components.Tools;
    using NetworkTools.Systems.Tools;

    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Jobs;
    using Unity.Collections;

    public partial class NT_RoadShapeToolSystem {
        /// <inheritdoc />
        public override IReadOnlyList<HintTooltipEntry> GetHintTooltips(
            OperationPhase phase,
            ProxyAction    applyAction,
            ProxyAction    secondaryApplyAction) {
            return phase switch {
                OperationPhase.Idle => new HintTooltipEntry[] {
                    new("NetworkTools.HintTooltip.ShapeSlope.SelectStart", applyAction),
                    new("NetworkTools.HintTooltip.Common.Exit", secondaryApplyAction)
                },
                OperationPhase.Configuring => new HintTooltipEntry[] {
                    new("NetworkTools.HintTooltip.ShapeSlope.SelectSecond", applyAction),
                    new("NetworkTools.HintTooltip.ShapeSlope.RemoveLast", secondaryApplyAction)
                },
                OperationPhase.Ready => new HintTooltipEntry[] {
                    new("NetworkTools.HintTooltip.ShapeSlope.ExtendPath", applyAction),
                    new("NetworkTools.HintTooltip.ShapeSlope.RemoveLast", secondaryApplyAction)
                },
                _ => System.Array.Empty<HintTooltipEntry>()
            };
        }
        public override bool TrySetPrefab(PrefabBase prefab) {
            var hasShapeSlope = m_PrefabSystem.HasComponent<NT_ShapeSlope>(prefab);
            var hasShapeCurve = m_PrefabSystem.HasComponent<NT_ShapeCurve>(prefab);
            m_Log.Debug(
                $"TrySetPrefab {prefab is NT_ToolPrefab} hasShapeSlope={hasShapeSlope} hasShapeCurve={hasShapeCurve}");
            var validRequest =
                prefab is NT_ToolPrefab &&
                (hasShapeSlope || hasShapeCurve);

            if (!validRequest)
            {
                return false;
            }

            // Detect variant switch (slope ↔ curve) and reset config/handles
            var wasSlopePrefab = m_Prefab != null && m_PrefabSystem.HasComponent<NT_ShapeSlope>(m_Prefab);
            var wasCurvePrefab = m_Prefab != null && m_PrefabSystem.HasComponent<NT_ShapeCurve>(m_Prefab);

            m_Prefab = prefab;

            if (hasShapeSlope && !wasSlopePrefab) {
                SetTransformationConfig(ShapeTransformConfig.SlopeLinear());
            } else if (hasShapeCurve && !wasCurvePrefab) {
                SetTransformationConfig(ShapeTransformConfig.CurveStraighten());
            }

            return true;
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_Log.Prefix = nameof(NT_RoadShapeToolSystem);

            // Configuration
            RenderEligibleNodes      = true;
            RenderHandles            = true;
            DisableVanillaValidation = true;

            // Initialize selection state (base class NativeLists)
            InitializeSelectionState();

            // Cached path data for handles and jobs
            m_EdgeStates = new NativeList<EdgeState>(32, Allocator.Persistent);
            m_NodeStates = new NativeList<NodeState>(33, Allocator.Persistent);
            m_PathDataValid = false;
        }

        protected override void OnDestroy() {
            // Dispose selection state (base class NativeLists)
            DisposeSelectionState();

            // Dispose cached path data
            if (m_EdgeStates.IsCreated) {
                m_EdgeStates.Dispose();
            }

            if (m_NodeStates.IsCreated) {
                m_NodeStates.Dispose();
            }

            base.OnDestroy();
        }

        protected override void OnStartRunning() {
            base.OnStartRunning();

            // Reset internal state
            m_LastHitPosition = default;
            Phase = OperationPhase.Idle;

            // Initialize selection state (makes all nodes eligible)
            ResetToNoSelection();
        }

        protected override void OnStopRunning() {
            base.OnStopRunning();

            // Clear selection state
            ClearSelectionState(false);

            // Invalidate cached path data
            InvalidatePathData();
        }

        public void MarkDirty() {
            m_UpdateNeeded = true;
        }

        /// <summary>
        ///     Sets a new transformation.
        /// </summary>
        public void SetTransformationConfig(ShapeTransformConfig config) {
            ShapeTransformConfig = config;
            m_UpdateNeeded       = true;

            // Enable/Disable rendering based on config
            RenderSlopeTooltips = config.RenderSlopeTooltips;

            // RE-INITIALIZE: Config changed while in Ready phase
            if (Phase == OperationPhase.Ready)
            {
                // InitializeConfig the transform (computes any needed initial values into config)
                InitializeCurrentTransform();

                // Re-create handles using the initialized config
                RefreshTransformHandles();
            }

            m_Log.Debug(
                $"Transformation config set: ShapeTemplate={config.Template}");
        }

        /// <summary>
        ///     Configures the transformation from the UI.
        /// </summary>
        public void UpdateTransformationConfig(ShapeTransformConfig config) {
            ShapeTransformConfig = config;
            m_UpdateNeeded       = true;

            m_Log.Debug(
                $"Transformation config updated: ShapeTemplate={config.Template}");
        }
    }
}
