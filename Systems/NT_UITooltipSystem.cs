// <copyright file="NT_UITooltipSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Colossal.Entities;

namespace NetworkTools.Systems {
    #region Using Statements

    using System.Collections.Generic;
    using System.Linq;
    using Colossal.Logging;
    using Colossal.Mathematics;
    using Colossal.UI.Binding;
    using Game;
    using Game.Input;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI;
    using Game.UI.Tooltip;
    using Game.UI.Widgets;
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
        private EntityQuery                      m_SelectedEdgesQuery;
        private EntityQuery                      m_TempEdgesQuery;
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

            // Tooltips for segments
            if (tool.ShowTooltipsSlopes) {
                foreach (var edgeEntity in m_SelectedEdgesQuery.ToEntityArray(Allocator.Temp)) {
                    var curve = EntityManager.GetComponentData<Curve>(edgeEntity);
                    var curveLength = curve.m_Length;

                    // Ignore short segments
                    if (curveLength < 8f) {
                        continue;
                    }

                    activeEdges.Add(edgeEntity);

                    var edge = EntityManager.GetComponentData<Edge>(edgeEntity);

                    // Determine actual traversal direction using PathIndex
                    Entity actualStart, actualEnd;
                    if (EntityManager.TryGetComponent<NT_Selected>(edge.m_Start, out var startSel) &&
                        EntityManager.TryGetComponent<NT_Selected>(edge.m_End, out var endSel)) {
                        if (startSel.PathIndex < endSel.PathIndex) {
                            actualStart = edge.m_Start;
                            actualEnd = edge.m_End;
                        } else {
                            actualStart = edge.m_End;
                            actualEnd = edge.m_Start;
                        }
                    } else {
                        actualStart = edge.m_Start;
                        actualEnd = edge.m_End;
                    }

                    var startNode = EntityManager.GetComponentData<Node>(actualStart);
                    var endNode = EntityManager.GetComponentData<Node>(actualEnd);
                    var yStart = startNode.m_Position.y;
                    var yEnd = endNode.m_Position.y;
                    var deltaY = yEnd - yStart;
                    var slopePercent = deltaY / curveLength * 100f;

                    var newPosition = WorldToTooltipPos(MathUtils.Position(curve.m_Bezier, 0.5f), out var isOnscreen);

                    // Get or create cached tooltip group
                    if (!m_EdgeTooltipCache.TryGetValue(edgeEntity, out var tooltipGroup)) {
                        var slopeTooltip = new FloatTooltip {
                            icon = "Media/Glyphs/Slope.svg",
                            unit = "percentageSingleFraction",
                            signed = true,
                            value = slopePercent,
                        };

                        tooltipGroup = new TooltipGroup {
                            path = $"NT_Edge_{edgeEntity.Index}_{edgeEntity.Version}",
                            horizontalAlignment = TooltipGroup.Alignment.Center,
                            verticalAlignment = TooltipGroup.Alignment.Center,
                            category = TooltipGroup.Category.Network,
                            position = newPosition,
                        };
                        tooltipGroup.children.Add(slopeTooltip);
                        m_EdgeTooltipCache[edgeEntity] = tooltipGroup;
                    } else {
                        // Update cached tooltip
                        var slopeTooltip = (FloatTooltip)tooltipGroup.children[0];
                        if (!Mathf.Approximately(slopeTooltip.value, slopePercent)) {
                            slopeTooltip.value = slopePercent;
                            tooltipGroup.SetChildrenChanged();
                        }

                        if (!tooltipGroup.position.Equals(newPosition)) {
                            tooltipGroup.position = newPosition;
                            tooltipGroup.SetChildrenChanged();
                        }
                    }

                    if (tooltipGroup.children.Count > 0) {
                        AddGroup(tooltipGroup);
                    }
                }
            }

            // Clean up stale edge tooltips
            var edgesToRemove = new List<Entity>();
            foreach (var key in m_EdgeTooltipCache.Keys) {
                if (!activeEdges.Contains(key)) {
                    edgesToRemove.Add(key);
                }
            }

            foreach (var key in edgesToRemove) {
                m_EdgeTooltipCache.Remove(key);
            }

            activeEdges.Dispose();
        }

        private void UpdateNodeTooltips(NT_BaseToolSystem tool) {
            var activeNodes = new NativeHashSet<Entity>(32, Allocator.Temp);

            foreach (var entity in m_SelectedNodesQuery.ToEntityArray(Allocator.Temp)) {
                activeNodes.Add(entity);

                var node = EntityManager.GetComponentData<Node>(entity);
                var newPosition = WorldToTooltipPos(node.m_Position, out var isOnScreen);

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
                var hasFirst = EntityManager.HasComponent<NT_SelectedFirst>(entity);
                var hasLast = EntityManager.HasComponent<NT_SelectedLast>(entity);

                var expectedChildren = (hasFirst ? 1 : 0) + (hasLast ? 1 : 0);
                if (group.children.Count != expectedChildren) {
                    group.children.Clear();

                    if (hasFirst) {
                        group.children.Add(new StringTooltip { value = "Start Node" });
                    }

                    if (hasLast) {
                        group.children.Add(new StringTooltip { value = "End Node" });
                    }

                    group.SetChildrenChanged();
                }

                if (group.children.Count > 0) {
                    AddGroup(group);
                }
            }

            // Clean up stale node tooltips
            var nodesToRemove = new List<Entity>();
            foreach (var key in m_NodeTooltipCache.Keys) {
                if (!activeNodes.Contains(key)) {
                    nodesToRemove.Add(key);
                }
            }

            foreach (var key in nodesToRemove) {
                m_NodeTooltipCache.Remove(key);
            }

            activeNodes.Dispose();
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

        private static float2 WorldToTooltipPos(Vector3 worldPos, out bool onScreen) {
            var xy = new float2(Camera.main.WorldToScreenPoint(worldPos).x, Camera.main.WorldToScreenPoint(worldPos).y);
            xy.y     = (float)Screen.height - xy.y;
            onScreen = xy.x >= 0f && xy.y >= 0f && xy.x <= (float)Screen.width && xy.y <= (float)Screen.height;
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