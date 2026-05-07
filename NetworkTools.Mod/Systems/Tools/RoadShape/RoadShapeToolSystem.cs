namespace NetworkTools.Systems.Tools.RoadShape {
    using System.Collections.Generic;

    using Game.Input;

    using NetworkTools.Systems.Handles;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.Parameters;

    using Unity.Burst;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Tool system for reshaping road segments.
    ///     Allows selecting a contiguous path of road nodes and applying transformations.
    /// </summary>
    public partial class NT_RoadShapeToolSystem : NT_PathSelectionToolSystem, IManualApplyProvider {
        /// <inheritdoc />
        public override string toolID => "RoadShapeTool";

        // ── Parameters 

        public EnumParameter<ShapeTransformTemplate> Template        = new("roadShape.template", ShapeTransformTemplate.Preserve);
        public FloatParameter                        EaseInLength    = new("roadShape.easeInLength",    0.1f, 0f, 0.5f, modes: (int)ShapeTransformTemplate.SlopeEaseInOut) {
            Handles = new IHandleSpec<float>[] {
                new ComputedPositionHandle {
                    ComputePosition = (tool, value) => {
                        var t = (NT_RoadShapeToolSystem)tool;
                        var start = t.m_ShapeTransformContext.StartPosition;
                        var end   = t.m_ShapeTransformContext.EndPosition;
                        var pos   = math.lerp(start, end, value);
                        pos.y = start.y + 1f;
                        return pos;
                    },
                    ComputeFromPosition = (tool, pos) => {
                        var t    = (NT_RoadShapeToolSystem)tool;
                        var start = t.m_ShapeTransformContext.StartPosition;
                        var end   = t.m_ShapeTransformContext.EndPosition;
                        var path  = end.xz - start.xz;
                        var len   = math.length(path);
                        if (len < 0.001f) return t.EaseInLength.Min;
                        var axis   = path / len;
                        var offset = pos.xz - start.xz;
                        return math.clamp(math.dot(offset, axis) / len, t.EaseInLength.Min, t.EaseInLength.Max);
                    }
                }
            }
        };
        public FloatParameter                        EaseOutLength   = new("roadShape.easeOutLength",   0.1f, 0f, 0.5f, modes: (int)ShapeTransformTemplate.SlopeEaseInOut) {
            Handles = new IHandleSpec<float>[] {
                new ComputedPositionHandle {
                    ComputePosition = (tool, value) => {
                        var t   = (NT_RoadShapeToolSystem)tool;
                        var start = t.m_ShapeTransformContext.StartPosition;
                        var end   = t.m_ShapeTransformContext.EndPosition;
                        var pos   = math.lerp(end, start, value);
                        pos.y = end.y + 1f;
                        return pos;
                    },
                    ComputeFromPosition = (tool, pos) => {
                        var t    = (NT_RoadShapeToolSystem)tool;
                        var start = t.m_ShapeTransformContext.StartPosition;
                        var end   = t.m_ShapeTransformContext.EndPosition;
                        var path  = start.xz - end.xz;
                        var len   = math.length(path);
                        if (len < 0.001f) return t.EaseOutLength.Min;
                        var axis   = path / len;
                        var offset = pos.xz - end.xz;
                        return math.clamp(math.dot(offset, axis) / len, t.EaseOutLength.Min, t.EaseOutLength.Max);
                    }
                }
            }
        };
        public FloatParameter                        ArchHeight      = new("roadShape.archHeight",      0.5f, -1f, 1f,  modes: (int)ShapeTransformTemplate.SlopeArch);
        public FloatParameter                        ArchPosition    = new("roadShape.archPosition",    0.5f, 0.1f, 0.9f, modes: (int)ShapeTransformTemplate.SlopeArch);
        public FloatParameter                        SmoothingFactor = new("roadShape.smoothingFactor", 0.5f, 0f, 1f,   modes: (int)ShapeTransformTemplate.CurveSmooth);

        /// <inheritdoc />
        protected override int GetActiveModeFlag() => (int)Template.Value;

        // ── Non-parameter state

        /// <summary>
        ///     Caches the last hit position for tool-specific use.
        /// </summary>
        private float3 m_LastHitPosition;

        #region Template Method Implementations

        /// <inheritdoc />
        protected override void OnPathReady() {
            RefreshPathData();
            RefreshTransformHandles();
        }

        /// <inheritdoc />
        protected override void OnSelectionCleared() {
            DestroyAllHandles();
            InvalidatePathData();
        }

        /// <inheritdoc />
        protected override void OnPathExtended(Entity newEndNode) {
            RefreshPathData();
            RefreshTransformHandles();
        }

        /// <inheritdoc />
        protected override void OnPathTrimmed(Entity newEndNode) {
            RefreshPathData();
            RefreshTransformHandles();
        }

        #endregion

        /// <summary>
        ///     Builds a Burst-compatible snapshot from the current parameter values.
        /// </summary>
        internal ShapeJobConfig BuildJobConfig() {
            return new ShapeJobConfig {
                Template        = Template.Value,
                EaseInLength    = EaseInLength.Value,
                EaseOutLength   = EaseOutLength.Value,
                ArchHeight      = ArchHeight.Value,
                ArchPosition    = ArchPosition.Value,
                SmoothingFactor = SmoothingFactor.Value,
            };
        }

        public override IReadOnlyList<HintTooltipEntry> GetHintTooltips(
    OperationPhase phase,
    ProxyAction applyAction,
    ProxyAction secondaryApplyAction) {
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
    }
}
