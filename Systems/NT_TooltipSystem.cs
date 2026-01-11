// <copyright file="NT_TooltipSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    using System.Collections.Generic;
    using Unity.Collections;

    #region Using Statements

    using Game.Common;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI.Tooltip;
    using Unity.Entities;
    using Unity.Mathematics;
    using static Game.Rendering.GuideLinesSystem;
    using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;
    using static NetworkTools.Systems.NT_UISystem;
    using Game.Routes;
    using Unity.Entities.UniversalDelegates;
    using Colossal.Mathematics;
    using System.Security.Cryptography;

    #endregion

    /// <summary>
    /// System responsible for UI Bindings & Lookup Handling.
    /// </summary>
    public partial class NT_TooltipSystem : TooltipSystemBase {
        private NT_ContiguousEdgeSelectionToolSystem m_NodeSelectionToolSystem;
        private EntityQuery m_SelectedEdgesQuery;
        private TooltipGroup m_GSelectedEdge;
        private FloatTooltip m_TSlope;
        private ToolSystem m_ToolSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_NodeSelectionToolSystem = World.GetOrCreateSystemManaged<NT_ContiguousEdgeSelectionToolSystem>();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            // Queries
            m_SelectedEdgesQuery = SystemAPI.QueryBuilder()
                .WithAll<Edge, NT_Selected>()
                .Build();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            if (m_ToolSystem.activeTool is not NT_BaseToolSystem tool) {
                return;
            }

            // Tooltips for segments
            if (tool.ShowTooltipsSlopes) {
                foreach (var edgeEntity in m_SelectedEdgesQuery.ToEntityArray(Allocator.Temp)) {
                    var curve = EntityManager.GetComponentData<Curve>(edgeEntity);
                    var curveLength = curve.m_Length;

                    // Ignore short segments
                    if (curveLength < 8f) {
                        continue;
                    }

                    var edge = EntityManager.GetComponentData<Edge>(edgeEntity);
                    var startNode = EntityManager.GetComponentData<Node>(edge.m_Start);
                    var endNode = EntityManager.GetComponentData<Node>(edge.m_End);
                    var yStart = startNode.m_Position.y;
                    var yEnd = endNode.m_Position.y;
                    var deltaY = yEnd - yStart;
                    var slopePercent = (deltaY / curveLength) * 100f;

                    // Create tooltip group for selected edge
                    var tooltipGroup = new TooltipGroup {
                        path = $"NT_Edge_{edgeEntity}",
                        horizontalAlignment = TooltipGroup.Alignment.Center,
                        verticalAlignment = TooltipGroup.Alignment.Center,
                        category = TooltipGroup.Category.Network,
                        position = TooltipSystemBase.WorldToTooltipPos(MathUtils.Position(curve.m_Bezier, 0.5f), out var isOnscreen),
                    };
                    var slopeTooltip = new FloatTooltip {
                        icon = "Media/Glyphs/Slope.svg",
                        unit = "percentageSingleFraction",
                        signed = true,
                        value = slopePercent
                    };
                    tooltipGroup.children.Add(slopeTooltip);

                    // Add or update the tooltips
                    base.AddGroup(tooltipGroup);
                }
            }

            foreach (var entity in SystemAPI.
                QueryBuilder()
                .WithAll<Node>()
                .WithAny<NT_Highlighted, NT_Eligible, NT_Selected, NT_SelectedFirst, NT_SelectedLast>()
                .Build()
                .ToEntityArray(Allocator.Temp)) {
                var node = EntityManager.GetComponentData<Node>(entity);
                var position = WorldToTooltipPos(node.m_Position, out var isOnScreen);

                var group = new TooltipGroup {
                    position = position,
                    path = $"NT_Highlighted_group_{entity}",
                    horizontalAlignment = TooltipGroup.Alignment.Center,
                    verticalAlignment = TooltipGroup.Alignment.Center,
                    category = TooltipGroup.Category.Network,
                    children = { },
                };

                //if (EntityManager.HasComponent<NT_Highlighted>(entity)) {
                //    var tooltip = new StringTooltip() {
                //        value = "Highlighted",
                //    };
                //    group.children.Add(tooltip);
                //}
                //if (EntityManager.HasComponent<NT_Eligible>(entity)) {
                //    var tooltip = new StringTooltip() {
                //        value = "Eligible",
                //    };
                //    group.children.Add(tooltip);
                //}
                //if (EntityManager.HasComponent<NT_Selected>(entity)) {
                //    var tooltip = new StringTooltip() {
                //        value = "Selected",
                //    };
                //    group.children.Add(tooltip);
                //}
                if (EntityManager.HasComponent<NT_SelectedFirst>(entity)) {
                    var tooltip = new StringTooltip() {
                        value = "Start Node",
                    };
                    group.children.Add(tooltip);
                }
                if (EntityManager.HasComponent<NT_SelectedLast>(entity)) {
                    var tooltip = new StringTooltip() {
                        value = "End Node",
                    };
                    group.children.Add(tooltip);
                }

                base.AddGroup(group);
            }
        }

        private static float3 GetWorldPosition(NativeList<NetCourse> courses, float length) {
            var num = -length;

            foreach (NetCourse netCourse in courses) {
                num += netCourse.m_Length;
                if (num >= 0f && netCourse.m_Length != 0f) {
                    float num2 = math.lerp(netCourse.m_StartPosition.m_CourseDelta, netCourse.m_EndPosition.m_CourseDelta, 1f - num / netCourse.m_Length);
                    return MathUtils.Position(netCourse.m_Curve, num2);
                }
            }

            return courses[courses.Length - 1].m_EndPosition.m_Position;
        }
    }
}