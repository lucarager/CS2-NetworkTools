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
            foreach (var entity in SystemAPI.
                QueryBuilder()
                .WithAny<NT_Highlighted, NT_Eligible, NT_Selected, NT_SelectedFirst, NT_SelectedLast>()
                .Build()
                .ToEntityArray(Allocator.Temp)) {
                var node = EntityManager.GetComponentData<Node>(entity);
                var position = WorldToTooltipPos(node.m_Position, out var isOnScreen);
                position.y += 15f;

                var group = new TooltipGroup {
                    position = position,
                    path = $"NT_Highlighted_group_{entity}",
                    horizontalAlignment = TooltipGroup.Alignment.End,
                    verticalAlignment = TooltipGroup.Alignment.End,
                    category = TooltipGroup.Category.Network,
                    children = {},
                };

                if (EntityManager.HasComponent<NT_Highlighted>(entity)) {
                    var tooltip = new StringTooltip() {
                        value = "Highlighted",
                    };
                    group.children.Add(tooltip);
                }
                if (EntityManager.HasComponent<NT_Eligible>(entity)) {
                    var tooltip = new StringTooltip() {
                        value = "Eligible",
                    };
                    group.children.Add(tooltip);
                }
                if (EntityManager.HasComponent<NT_Selected>(entity)) {
                    var tooltip = new StringTooltip() {
                        value = "Selected",
                    };
                    group.children.Add(tooltip);
                }
                if (EntityManager.HasComponent<NT_SelectedFirst>(entity)) {
                    var tooltip = new StringTooltip() {
                        value = "Selected First",
                    };
                    group.children.Add(tooltip);
                }
                if (EntityManager.HasComponent<NT_SelectedLast>(entity)) {
                    var tooltip = new StringTooltip() {
                        value = "Selected Last",
                    };
                    group.children.Add(tooltip);
                }
               
                base.AddGroup(group);
            }
        }
    }
}