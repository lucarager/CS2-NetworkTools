// <copyright file="NT_UITooltipSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
#region Using Statements

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

    #endregion

    /// <summary>
    /// System responsible for UI Bindings & Lookup Handling.
    /// </summary>
    public partial class NT_UITooltipSystem : UISystemBase {
        private const float MinCurveLength = 8f;
        private const float TempTooltipYOffset = 20f;

        private EntityQuery                      m_SelectedEdgesQuery;
        private EntityQuery                      m_SelectedNodesQuery;
        private ToolSystem                       m_ToolSystem;
        private WidgetBindings                   m_WidgetBindings;
        private PrefixedLogger                   m_Log;
        private List<TooltipGroup>               Groups { get; set; }
        private Dictionary<Entity, TooltipGroup> m_EdgeTooltipCache;
        private Dictionary<Entity, TooltipGroup> m_NodeTooltipCache;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            m_EdgeTooltipCache = new Dictionary<Entity, TooltipGroup>();
            m_NodeTooltipCache = new Dictionary<Entity, TooltipGroup>();

            // Queries
            m_SelectedEdgesQuery = SystemAPI.QueryBuilder()
                                            .WithAll<Edge>()
                                            .WithAny<NT_Selected, Temp>()
                                            .Build();
            m_SelectedNodesQuery = SystemAPI.QueryBuilder()
                                            .WithAll<Node>()
                                            .WithAny<NT_Highlighted, NT_Eligible, NT_Selected,
                                                NT_SelectedFirst, NT_SelectedLast>()
                                            .Build();

            // Data
            m_Log = new PrefixedLogger(nameof(NT_UITooltipSystem));
            m_Log.Debug("OnCreate()");
            AddUpdateBinding(m_WidgetBindings = new WidgetBindings("NT_tooltip", "groups"));
            Groups = new List<TooltipGroup>();
        }

        /// <inheritdoc/>
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

            if (tool.ShowTooltipsSlopes) {
                ProcessEdgeTooltips(activeEdges);
            }

            CleanupStaleEntries(m_EdgeTooltipCache, activeEdges);
            activeEdges.Dispose();
        }

        private void ProcessEdgeTooltips(NativeHashSet<Entity> activeEdges) {
            var edgeEntities = m_SelectedEdgesQuery.ToEntityArray(Allocator.Temp);

            foreach (var edgeEntity in edgeEntities) {
                var curve = EntityManager.GetComponentData<Curve>(edgeEntity);

                if (curve.m_Length < MinCurveLength) {
                    continue;
                }

                activeEdges.Add(edgeEntity);

                var isTemp = EntityManager.HasComponent<Temp>(edgeEntity);
                var (slopePercent, position) = CalculateEdgeSlopeData(edgeEntity, curve, isTemp);
                var tooltipGroup = GetOrCreateEdgeTooltip(edgeEntity, slopePercent, position, isTemp);

                if (tooltipGroup.children.Count > 0) {
                    AddGroup(tooltipGroup);
                }
            }

            edgeEntities.Dispose();
        }

        private (float slopePercent, float2 position) CalculateEdgeSlopeData(Entity edgeEntity, Curve curve, bool isTemp) {
            var edge = EntityManager.GetComponentData<Edge>(edgeEntity);
            var (actualStart, actualEnd) = DetermineTraversalDirection(edge);

            // Calculate deltaY from the bezier curve itself, not from node positions
            // This ensures we get the correct slope for transformed temp edges
            bool isForward = (actualStart == edge.m_Start);
            float startY = isForward ? curve.m_Bezier.a.y : curve.m_Bezier.d.y;
            float endY = isForward ? curve.m_Bezier.d.y : curve.m_Bezier.a.y;
            var deltaY = endY - startY;
            var slopePercent = deltaY / curve.m_Length * 100f;

            var position = WorldToTooltipPos(MathUtils.Position(curve.m_Bezier, 0.5f));
            var offset = isTemp ? TempTooltipYOffset : -TempTooltipYOffset;
            position.y += offset;

            return (slopePercent, position);
        }

        private (Entity start, Entity end) DetermineTraversalDirection(Edge edge) {
            if (EntityManager.TryGetComponent<NT_Selected>(edge.m_Start, out var startSel) &&
                EntityManager.TryGetComponent<NT_Selected>(edge.m_End, out var endSel)) {
                return startSel.PathIndex < endSel.PathIndex
                    ? (edge.m_Start, edge.m_End)
                    : (edge.m_End, edge.m_Start);
            }

            return (edge.m_Start, edge.m_End);
        }

        private TooltipGroup GetOrCreateEdgeTooltip(Entity edgeEntity, float slopePercent, float2 position, bool isTemp) {
            if (!m_EdgeTooltipCache.TryGetValue(edgeEntity, out var tooltipGroup)) {
                tooltipGroup = CreateEdgeTooltipGroup(edgeEntity, slopePercent, position, isTemp);
                m_EdgeTooltipCache[edgeEntity] = tooltipGroup;
            } else {
                UpdateEdgeTooltipGroup(tooltipGroup, slopePercent, position);
            }

            return tooltipGroup;
        }

        private static TooltipGroup CreateEdgeTooltipGroup(Entity edgeEntity, float slopePercent, float2 position, bool isTemp) {
            var path = $"NT_Edge_{edgeEntity.Index}_{edgeEntity.Version}";
            var fullPath = isTemp ? $"{path}*" : path;

            var slopeTooltip = new FloatTooltip {
                icon = "Media/Glyphs/Slope.svg",
                unit = "percentageSingleFraction",
                signed = true,
                value = slopePercent,
                color = isTemp ? TooltipColor.Success : TooltipColor.Info,
            };

            var tooltipGroup = new TooltipGroup {
                path                = fullPath,
                horizontalAlignment = isTemp ? TooltipGroup.Alignment.Start : TooltipGroup.Alignment.End,
                verticalAlignment   = isTemp ? TooltipGroup.Alignment.Start : TooltipGroup.Alignment.End,
                category            = TooltipGroup.Category.Network,
                position            = position,
            };
            tooltipGroup.children.Add(slopeTooltip);

            return tooltipGroup;
        }

        private static void UpdateEdgeTooltipGroup(TooltipGroup tooltipGroup, float slopePercent, float2 position) {
            var slopeTooltip = (FloatTooltip)tooltipGroup.children[0];

            if (!Mathf.Approximately(slopeTooltip.value, slopePercent)) {
                slopeTooltip.value = slopePercent;
                tooltipGroup.SetChildrenChanged();
            }

            if (!tooltipGroup.position.Equals(position)) {
                tooltipGroup.position = position;
                tooltipGroup.SetChildrenChanged();
            }
        }

        private void UpdateNodeTooltips(NT_BaseToolSystem tool) {
            var activeNodes = new NativeHashSet<Entity>(32, Allocator.Temp);
            var nodeEntities = m_SelectedNodesQuery.ToEntityArray(Allocator.Temp);

            foreach (var entity in nodeEntities) {
                activeNodes.Add(entity);

                var node = EntityManager.GetComponentData<Node>(entity);
                var newPosition = WorldToTooltipPos(node.m_Position);

                // Get or create cached tooltip group
                if (!m_NodeTooltipCache.TryGetValue(entity, out var group)) {
                    group = new TooltipGroup {
                        position = newPosition,
                        path = $"NT_Node_{entity.Index}_{entity.Version}",
                        horizontalAlignment = TooltipGroup.Alignment.Center,
                        verticalAlignment = TooltipGroup.Alignment.Center,
                        category = TooltipGroup.Category.Network,
                        children = { },
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

        private static void CleanupStaleEntries(Dictionary<Entity, TooltipGroup> cache, NativeHashSet<Entity> activeEntities) {
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
            var xy = new float2(screenPoint.x, (float)Screen.height - screenPoint.y);
            return xy;
        }

        private void AddGroup(TooltipGroup group) {
            if (group.path != PathSegment.Empty && Groups.Any((TooltipGroup g) => g.path == group.path)) {
                m_Log.Error($"Trying to add tooltip group with duplicate path '{group.path}'");
                return;
            }

            Groups.Add(group);
        }
    }
}