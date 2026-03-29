namespace NetworkTools.Systems.Tooltips {
    using System.Collections.Generic;
    using System.Linq;
    using Colossal.Entities;
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
    ///     System responsible for UI Bindings & Lookup Handling.
    /// </summary>
    public partial class NT_UITooltipSystem : UISystemBase {
        private const float                            MinCurveLength     = 8f;
        private const float                            TempTooltipYOffset = 20f;
        private       Dictionary<Entity, TooltipGroup> m_EdgeTooltipCache;
        private       PrefixedLogger                   m_Log;
        private       Dictionary<Entity, TooltipGroup> m_NodeTooltipCache;

        private EntityQuery        m_SelectedEdgesQuery;
        private EntityQuery        m_SelectedNodesQuery;
        private EntityQuery        m_TempEdgesQuery;
        private ToolSystem         m_ToolSystem;
        private WidgetBindings     m_WidgetBindings;
        private List<TooltipGroup> Groups { get; set; }

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            m_EdgeTooltipCache = new Dictionary<Entity, TooltipGroup>();
            m_NodeTooltipCache = new Dictionary<Entity, TooltipGroup>();

            // Queries
            m_SelectedEdgesQuery = SystemAPI.QueryBuilder()
                                            .WithAll<Edge>()
                                            .WithAny<NT_Selected>()
                                            .Build();
            m_SelectedNodesQuery = SystemAPI.QueryBuilder()
                                            .WithAll<Node>()
                                            .WithAny<NT_Highlighted, NT_Eligible, NT_Selected,
                                                NT_SelectedFirst, NT_SelectedLast>()
                                            .Build();
            m_TempEdgesQuery = SystemAPI.QueryBuilder()
                                            .WithAll<Edge>()
                                            .WithAny<Temp>()
                                            .Build();

            // Data
            m_Log = new PrefixedLogger(nameof(NT_UITooltipSystem));
            m_Log.Debug("OnCreate()");
            AddUpdateBinding(m_WidgetBindings = new WidgetBindings("NT_tooltip", "groups"));
            Groups = new List<TooltipGroup>();
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            if (!m_WidgetBindings.active) {
                return;
            }

            m_WidgetBindings.children.Clear();
            Groups.Clear();

            UpdateTooltips();

            foreach (var group in Groups) {
                m_WidgetBindings.children.Add(group);
            }

            base.OnUpdate();
        }

        private void UpdateEdgeTooltips(NT_BaseToolSystem tool) {
            var activeEdges = new NativeHashSet<Entity>(32, Allocator.Temp);

            if (tool.RenderSlopeTooltips) {
                ProcessSlopeTooltips(activeEdges);
            }

            CleanupStaleEntries(m_EdgeTooltipCache, activeEdges);
            activeEdges.Dispose();
        }

        private void ProcessSlopeTooltips(NativeHashSet<Entity> activeEdges) {
            var edgeEntities = m_SelectedEdgesQuery.ToEntityArray(Allocator.Temp);

            if (!m_TempEdgesQuery.IsEmpty) {
                // Process temp edges
                var tempEdgeEntities = m_TempEdgesQuery.ToEntityArray(Allocator.Temp);
                foreach (var edgeEntity in tempEdgeEntities)
                {
                    var temp = EntityManager.GetComponentData<Temp>(edgeEntity);
                    var originalEdge = temp.m_Original;

                    // Ignore if original edge is not NT_Selected, meaning its in our current set
                    if (!edgeEntities.Contains(originalEdge))
                    {
                        continue;
                    }

                    var tempCurve = EntityManager.GetComponentData<Curve>(edgeEntity);
                    var (newSlopePercent, tempPosition) = CalculateEdgeSlopeData(tempCurve);
                    var originalCurve = EntityManager.GetComponentData<Curve>(originalEdge);
                    var (originalSlopePercent, originalPosition) = CalculateEdgeSlopeData(originalCurve);

                    // Mark edge
                    activeEdges.Add(edgeEntity);

                    var tooltipGroup = GetOrCreateSlopeTooltip(originalEdge, tempPosition, originalSlopePercent, newSlopePercent);
                    if (tooltipGroup.children.Count > 0)
                    {
                        AddGroup(tooltipGroup);
                    }
                }
            } else {
                // Process real edges
                foreach (var edgeEntity in edgeEntities) {
                    var curve  = EntityManager.GetComponentData<Curve>(edgeEntity);
                    var (slopePercent, position) = CalculateEdgeSlopeData(curve);

                    // Mark edge
                    activeEdges.Add(edgeEntity);

                    var tooltipGroup = GetOrCreateSlopeTooltip(edgeEntity, position, slopePercent);
                    if (tooltipGroup.children.Count > 0) {
                        AddGroup(tooltipGroup);
                    }
                }
            }


            edgeEntities.Dispose();
        }

        private (float slopePercent, float2 position) CalculateEdgeSlopeData(Curve curve) {
            // Calculate deltaY from the bezier curve itself, not from node positions
            // This ensures we get the correct slope for transformed temp edges
            var startY = curve.m_Bezier.a.y;
            var endY   = curve.m_Bezier.d.y;

            var deltaY       = math.abs(endY - startY);
            var slopePercent = deltaY / curve.m_Length * 100f;

            var position = WorldToTooltipPos(MathUtils.Position(curve.m_Bezier, 0.5f));

            return (slopePercent, position);
        }

        private TooltipGroup GetOrCreateSlopeTooltip(Entity edgeEntity, float2 position, float slopePercent,
                                                     float  newSlopePercent = float.NaN) {
            // Retrieve cached tooltip group
            var hasCached = m_EdgeTooltipCache.TryGetValue(edgeEntity, out var tooltipGroup);

            // If no cached group, create new one
            if (!hasCached) {
                tooltipGroup                   = CreateSlopeTooltipGroup(edgeEntity, position);
                m_EdgeTooltipCache[edgeEntity] = tooltipGroup;
            }

            // Update group with new data
            UpdateSlopeTooltipGroup(tooltipGroup, position, slopePercent, newSlopePercent);
            return tooltipGroup;
        }

        private static TooltipGroup CreateSlopeTooltipGroup(Entity edgeEntity, float2 position) {
            var tooltipGroup = new TooltipGroup {
                path     = $"NT_Slope_{edgeEntity.Index}_{edgeEntity.Version}",
                category = TooltipGroup.Category.Network,
                position = position
            };
            tooltipGroup.children.Add(new SlopeTooltip());

            return tooltipGroup;
        }

        private static void UpdateSlopeTooltipGroup(TooltipGroup tooltipGroup, float2 position, float slopePercent,
                                                    float        newSlopePercent = float.NaN) {
            var slopeTooltip = (SlopeTooltip)tooltipGroup.children[0];

            if (!Mathf.Approximately(slopeTooltip.CurrentSlope, slopePercent)) {
                slopeTooltip.CurrentSlope = slopePercent;
                tooltipGroup.SetChildrenChanged();
            }

            if (!Mathf.Approximately(slopeTooltip.NewSlope, newSlopePercent)) {
                slopeTooltip.NewSlope = newSlopePercent;
                tooltipGroup.SetChildrenChanged();
            }

            if (!tooltipGroup.position.Equals(position)) {
                tooltipGroup.position = position;
                tooltipGroup.SetChildrenChanged();
            }
        }
        private void UpdateNodeTooltips(NT_BaseToolSystem tool) {
            var activeNodes  = new NativeHashSet<Entity>(32, Allocator.Temp);
            var nodeEntities = m_SelectedNodesQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in nodeEntities) {
                activeNodes.Add(entity);

                var node        = EntityManager.GetComponentData<Node>(entity);
                var newPosition = WorldToTooltipPos(node.m_Position);

                // Get or create cached tooltip group
                if (!m_NodeTooltipCache.TryGetValue(entity, out var group)) {
                    group = new TooltipGroup {
                        position            = newPosition,
                        path                = $"NT_Node_{entity.Index}_{entity.Version}",
                        horizontalAlignment = TooltipGroup.Alignment.Center,
                        verticalAlignment   = TooltipGroup.Alignment.Center,
                        category            = TooltipGroup.Category.Network
                    };
                    m_NodeTooltipCache[entity] = group;
                } else {
                    if (!group.position.Equals(newPosition)) {
                        group.position = newPosition;
                        group.SetChildrenChanged();
                    }
                }

                // Update children based on current components
                //var hasFirst = EntityManager.HasComponent<NT_SelectedFirst>(entity);
                //var hasLast = EntityManager.HasComponent<NT_SelectedLast>(entity);
                //var hasSelected = EntityManager.HasComponent<NT_Selected>(entity);

                //var expectedChildren = (hasFirst ? 1 : 0) + (hasLast ? 1 : 0);
                //if (group.children.Count != expectedChildren) {

                //}

                group.children.Clear();

                if (EntityManager.HasComponent<NT_SelectedFirst>(entity)) {
                    group.children.Add(new StringTooltip { value = "Start" });
                }

                if (EntityManager.HasComponent<NT_SelectedLast>(entity)) {
                    group.children.Add(new StringTooltip { value = "End" });
                }

                //if (EntityManager.HasComponent<NT_Highlighted>(entity)) {
                //    group.children.Add(new StringTooltip { value = "Highlighted" });
                //}

                //if (EntityManager.HasComponent<NT_Eligible>(entity)) {
                //    group.children.Add(new StringTooltip { value = "Eligible" });
                //}

                //if (EntityManager.HasComponent<NT_Selected>(entity)) {
                //    group.children.Add(new StringTooltip { value = "Selected" });
                //}

                group.SetChildrenChanged();

                if (group.children.Count > 0) {
                    AddGroup(group);
                }
            }

            nodeEntities.Dispose();

            CleanupStaleEntries(m_NodeTooltipCache, activeNodes);
            activeNodes.Dispose();
        }

        private static void CleanupStaleEntries(Dictionary<Entity, TooltipGroup> cache,
                                                NativeHashSet<Entity>            activeEntities) {
            var entitiesToRemove = new List<Entity>();

            foreach (var key in cache.Keys) {
                if (!activeEntities.Contains(key)) {
                    entitiesToRemove.Add(key);
                }
            }

            foreach (var key in entitiesToRemove) {
                cache.Remove(key);
            }
        }

        private void UpdateTooltips() {
            if (m_ToolSystem.activeTool is not NT_BaseToolSystem tool) {
                // Clear caches when tool is not active
                m_EdgeTooltipCache.Clear();
                m_NodeTooltipCache.Clear();
                return;
            }

            UpdateEdgeTooltips(tool);
            UpdateNodeTooltips(tool);
        }

        private static float2 WorldToTooltipPos(Vector3 worldPos) {
            var screenPoint = Camera.main.WorldToScreenPoint(worldPos);
            var xy          = new float2(screenPoint.x, Screen.height - screenPoint.y);
            return xy;
        }

        private void AddGroup(TooltipGroup group) {
            if (group.path != PathSegment.Empty && Groups.Any(g => g.path == group.path)) {
                m_Log.Debug($"Trying to add tooltip group with duplicate path '{group.path}'");
                return;
            }

            Groups.Add(group);
        }
    }
}