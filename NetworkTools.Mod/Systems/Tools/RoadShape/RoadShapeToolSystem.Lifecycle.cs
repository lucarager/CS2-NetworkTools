namespace NetworkTools.Systems.Tools.RoadShape {
    using System.Collections.Generic;

    using Game.Common;
    using NetworkTools.Systems.Tools.Parameters;
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
        public override bool TrySetPrefab(PrefabBase prefab) {
            var hasShapeSlope = m_PrefabSystem.HasComponent<NT_ShapeSlopeTool>(prefab);
            var hasShapeCurve = m_PrefabSystem.HasComponent<NT_ShapeCurveTool>(prefab);
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
            var wasSlopePrefab = m_Prefab != null && m_PrefabSystem.HasComponent<NT_ShapeSlopeTool>(m_Prefab);
            var wasCurvePrefab = m_Prefab != null && m_PrefabSystem.HasComponent<NT_ShapeCurveTool>(m_Prefab);

            m_Prefab = prefab;

            if (hasShapeSlope && !wasSlopePrefab) {
                Template.Value = ShapeTransformTemplate.SlopeLinear;
            } else if (hasShapeCurve && !wasCurvePrefab) {
                Template.Value = ShapeTransformTemplate.CurveStraighten;
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

            // Template change additionally applies presets and reinitializes
            Template.OnChanged += _ => {
                ApplyTemplatePreset(Template.Value);
                if (Phase == OperationPhase.Ready) {
                    InitializeCurrentTransform();
                    RefreshTransformHandles();
                }
            };

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
        ///     Applies template-specific defaults when the template changes.
        /// </summary>
        private void ApplyTemplatePreset(ShapeTransformTemplate template) {
            var isSlopeTemplate = template == ShapeTransformTemplate.SlopeLinear ||
                                  template == ShapeTransformTemplate.SlopeEaseInOut ||
                                  template == ShapeTransformTemplate.SlopeArch;

            RenderSlopeTooltips = isSlopeTemplate;
            RenderNodeTooltips  = isSlopeTemplate;

            switch (template) {
                case ShapeTransformTemplate.SlopeEaseInOut:
                    EaseInLength.ResetToDefault();
                    EaseOutLength.ResetToDefault();
                    break;
                case ShapeTransformTemplate.SlopeArch:
                    ArchHeight.ResetToDefault();
                    ArchPosition.ResetToDefault();
                    break;
                case ShapeTransformTemplate.CurveSmooth:
                    SmoothingFactor.ResetToDefault();
                    break;
            }

            m_Log.Debug($"Template preset applied: {template}");
        }
    }
}
