namespace NetworkTools.Systems.Tooltips {
    using System.Collections.Generic;
    using Colossal.Mathematics;
    using Game.Net;
    using Game.Tools;
    using Game.UI;
    using Game.UI.Tooltip;
    using Game.UI.Widgets;
    using NetworkTools.Components;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Utils;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    ///     Builds tooltip groups for the active NT tool each frame and pushes them to the UI binding.
    /// </summary>
    public partial class NT_UITooltipSystem : UISystemBase {
        private Camera               m_Camera;
        private PrefixedLogger       m_Log;
        private HashSet<PathSegment> m_SeenPaths;
        private EntityQuery          m_SelectedEdgesQuery;
        private EntityQuery          m_SelectedNodesQuery;
        private EntityQuery          m_TempEdgesQuery;
        private ToolSystem           m_ToolSystem;
        private WidgetBindings       m_WidgetBindings;
        private List<TooltipGroup>   Groups { get; set; }

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            m_SelectedEdgesQuery = SystemAPI.QueryBuilder()
                                            .WithAll<Edge>()
                                            .WithAny<NT_Selected>()
                                            .Build();
            m_SelectedNodesQuery = SystemAPI.QueryBuilder()
                                            .WithAll<Node>()
                                            .WithAny<NT_SelectedFirst, NT_SelectedLast>()
                                            .Build();
            m_TempEdgesQuery = SystemAPI.QueryBuilder()
                                        .WithAll<Edge>()
                                        .WithAny<Temp>()
                                        .Build();

            m_Log = new PrefixedLogger(nameof(NT_UITooltipSystem));
            m_Log.Debug("OnCreate()");
            AddUpdateBinding(m_WidgetBindings = new WidgetBindings("NT_tooltip", "groups"));
            Groups      = new List<TooltipGroup>();
            m_SeenPaths = new HashSet<PathSegment>();
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            if (!m_WidgetBindings.active) {
                return;
            }

            m_WidgetBindings.children.Clear();
            Groups.Clear();
            m_SeenPaths.Clear();

            if (m_ToolSystem.activeTool is NT_BaseToolSystem tool) {
                BuildEdgeTooltips(tool);

                if (tool.RenderNodeTooltips) {
                    BuildNodeTooltips();
                }
            }

            foreach (var group in Groups) {
                m_WidgetBindings.children.Add(group);
            }

            base.OnUpdate();
        }

        private void BuildEdgeTooltips(NT_BaseToolSystem tool) {
            var contexts = CollectEdgeContexts();
            foreach (var ctx in contexts) {
                var position = WorldToTooltipPos(MathUtils.Position(ctx.CurrentCurve.m_Bezier, 0.5f));
                var group = new TooltipGroup {
                    path     = $"NT_Edge_{ctx.Edge.Index}_{ctx.Edge.Version}",
                    category = TooltipGroup.Category.Network,
                    position = position,
                };

                // Per-feature children — each gated on its own flag.
                if (tool.RenderSlopeTooltips) {
                    AppendSlopeTooltip(group, ctx);
                }

                if (tool.RenderLengthTooltips && ctx.HasPreview) {
                    AppendLengthTooltip(group, ctx);
                }

                if (group.children.Count > 0) {
                    AddGroup(group);
                }
            }
        }

        // Resolves the set of edges to render tooltips for, normalizing temp/non-temp into one shape.
        private List<EdgeTooltipContext> CollectEdgeContexts() {
            var contexts      = new List<EdgeTooltipContext>();
            var selectedEdges = m_SelectedEdgesQuery.ToEntityArray(Allocator.Temp);

            if (m_TempEdgesQuery.IsEmpty) {
                foreach (var edge in selectedEdges) {
                    var curve = EntityManager.GetComponentData<Curve>(edge);
                    contexts.Add(new EdgeTooltipContext {
                        Edge          = edge,
                        CurrentCurve  = curve,
                        OriginalCurve = curve,
                        HasPreview    = false,
                    });
                }
                selectedEdges.Dispose();
                return contexts;
            }

            var selectedSet = new NativeHashSet<Entity>(selectedEdges.Length, Allocator.Temp);
            foreach (var e in selectedEdges) {
                selectedSet.Add(e);
            }
            selectedEdges.Dispose();

            var tempEdges = m_TempEdgesQuery.ToEntityArray(Allocator.Temp);
            foreach (var tempEdge in tempEdges) {
                var temp = EntityManager.GetComponentData<Temp>(tempEdge);
                if (!selectedSet.Contains(temp.m_Original)) {
                    continue;
                }

                contexts.Add(new EdgeTooltipContext {
                    Edge          = temp.m_Original,
                    CurrentCurve  = EntityManager.GetComponentData<Curve>(tempEdge),
                    OriginalCurve = EntityManager.GetComponentData<Curve>(temp.m_Original),
                    HasPreview    = true,
                });
            }
            tempEdges.Dispose();
            selectedSet.Dispose();
            return contexts;
        }

        private static void AppendSlopeTooltip(TooltipGroup group, EdgeTooltipContext ctx) {
            var current  = ComputeSlopePercent(ctx.OriginalCurve);
            var newValue = ctx.HasPreview ? ComputeSlopePercent(ctx.CurrentCurve) : float.NaN;
            group.children.Add(new SlopeTooltip { CurrentSlope = current, NewSlope = newValue });
        }

        private static void AppendLengthTooltip(TooltipGroup group, EdgeTooltipContext ctx) {
            var length = MathUtils.Length(ctx.CurrentCurve.m_Bezier);
            group.children.Add(new FloatTooltip { value = length, unit = "m"});
        }

        private static float ComputeSlopePercent(Curve curve) {
            // Read deltaY from the bezier endpoints (not node positions) so transformed temp edges read correctly.
            var deltaY = math.abs(curve.m_Bezier.d.y - curve.m_Bezier.a.y);
            return deltaY / curve.m_Length * 100f;
        }

        private struct EdgeTooltipContext {
            public Entity Edge;
            public Curve  CurrentCurve;
            public Curve  OriginalCurve;
            public bool   HasPreview;
        }

        private void BuildNodeTooltips() {
            var nodes = m_SelectedNodesQuery.ToEntityArray(Allocator.Temp);
            foreach (var entity in nodes) {
                var node     = EntityManager.GetComponentData<Node>(entity);
                var position = WorldToTooltipPos(node.m_Position);

                var group = new TooltipGroup {
                    path                = $"NT_Node_{entity.Index}_{entity.Version}",
                    category            = TooltipGroup.Category.Network,
                    position            = position,
                    horizontalAlignment = TooltipGroup.Alignment.Center,
                    verticalAlignment   = TooltipGroup.Alignment.Center,
                };

                if (EntityManager.HasComponent<NT_SelectedFirst>(entity)) {
                    group.children.Add(new StringTooltip { value = "Start" });
                }

                if (EntityManager.HasComponent<NT_SelectedLast>(entity)) {
                    group.children.Add(new StringTooltip { value = "End" });
                }

                if (group.children.Count > 0) {
                    AddGroup(group);
                }
            }
            nodes.Dispose();
        }

        private float2 WorldToTooltipPos(Vector3 worldPos) {
            if (m_Camera == null) {
                if (Camera.main == null) {
                    return default;
                }

                m_Camera = Camera.main;
            }

            var screenPoint = m_Camera.WorldToScreenPoint(worldPos);
            return new float2(screenPoint.x, Screen.height - screenPoint.y);
        }

        private void AddGroup(TooltipGroup group) {
            if (group.path != PathSegment.Empty && !m_SeenPaths.Add(group.path)) {
                m_Log.Debug($"Trying to add tooltip group with duplicate path '{group.path}'");
                return;
            }

            Groups.Add(group);
        }
    }
}
