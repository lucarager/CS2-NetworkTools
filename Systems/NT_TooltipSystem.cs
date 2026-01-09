// <copyright file="NT_TooltipSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>



// <copyright file="P_UISystem.cs" company="Luca Rager">
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

    #endregion

    /// <summary>
    /// System responsible for UI Bindings & Lookup Handling.
    /// </summary>
    public partial class NT_TooltipSystem : TooltipSystemBase {
        private NT_ContiguousEdgeSelectionToolSystem m_NodeSelectionToolSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            m_NodeSelectionToolSystem = World.GetOrCreateSystemManaged<NT_ContiguousEdgeSelectionToolSystem>();

            base.OnCreate();
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            // todo add groups
            // todo split this way up!
            var selectedNodes = m_NodeSelectionToolSystem.GetSelectedNodes();

            for (var i = 0; i < selectedNodes.Length; i++) {
                var nodeEntity = selectedNodes[i];
                var node       = EntityManager.GetComponentData<Node>(nodeEntity);
                var position   = WorldToTooltipPos(node.m_Position, out var isOnScreen);

                var tooltip = new StringTooltip() {
                    value = $"Node {i}"
                };

                var group = new TooltipGroup {
                    position            = position,
                    path                = $"NT_group{i}",
                    horizontalAlignment = TooltipGroup.Alignment.Center,
                    verticalAlignment   = TooltipGroup.Alignment.Center,
                    category            = TooltipGroup.Category.Network,
                    children = {
                        tooltip,
                    },
                };

                base.AddGroup(group);
            }

            foreach (var entity in SystemAPI.QueryBuilder().WithAll<NT_Highlighted>().Build().ToEntityArray(Allocator.Temp)) {
                var node = EntityManager.GetComponentData<Node>(entity);
                var position = WorldToTooltipPos(node.m_Position, out var isOnScreen);
                position.y += 50f;

                base.AddGroup(new TooltipGroup {
                    position = position,
                    path = $"NT_Highlighted_group_{entity}",
                    horizontalAlignment = TooltipGroup.Alignment.Center,
                    verticalAlignment = TooltipGroup.Alignment.Center,
                    category = TooltipGroup.Category.Network,
                    children = {
                        new StringTooltip() {
                            value = $"NT_Highlighted"
                        },
                    },
                });
            }

            foreach (var entity in SystemAPI.QueryBuilder().WithAll<NT_Eligible>().Build().ToEntityArray(Allocator.Temp)) {
                var node = EntityManager.GetComponentData<Node>(entity);
                var position = WorldToTooltipPos(node.m_Position, out var isOnScreen);
                position.y += 100f;

                base.AddGroup(new TooltipGroup {
                    position = position,
                    path = $"NT_Eligible_group_{entity}",
                    horizontalAlignment = TooltipGroup.Alignment.Center,
                    verticalAlignment = TooltipGroup.Alignment.Center,
                    category = TooltipGroup.Category.Network,
                    children = {
                        new StringTooltip() {
                            value = $"NT_Eligible"
                        },
                    },
                });
            }

            foreach (var entity in SystemAPI.QueryBuilder().WithAll<NT_SelectedFirst>().Build().ToEntityArray(Allocator.Temp)) {
                var node = EntityManager.GetComponentData<Node>(entity);
                var position = WorldToTooltipPos(node.m_Position, out var isOnScreen);
                position.y += 150f;

                base.AddGroup(new TooltipGroup {
                    position = position,
                    path = $"NT_SelectedFirst_group_{entity}",
                    horizontalAlignment = TooltipGroup.Alignment.Center,
                    verticalAlignment = TooltipGroup.Alignment.Center,
                    category = TooltipGroup.Category.Network,
                    children = {
                        new StringTooltip() {
                            value = $"NT_SelectedFirst"
                        },
                    },
                });
            }

            foreach (var entity in SystemAPI.QueryBuilder().WithAll<NT_SelectedLast>().Build().ToEntityArray(Allocator.Temp)) {
                var node = EntityManager.GetComponentData<Node>(entity);
                var position = WorldToTooltipPos(node.m_Position, out var isOnScreen);
                position.y += 200f;

                base.AddGroup(new TooltipGroup {
                    position = position,
                    path = $"NT_SelectedLast_group_{entity}",
                    horizontalAlignment = TooltipGroup.Alignment.Center,
                    verticalAlignment = TooltipGroup.Alignment.Center,
                    category = TooltipGroup.Category.Network,
                    children = {
                        new StringTooltip() {
                            value = $"NT_SelectedLast"
                        },
                    },
                });
            }
        }
    }
}