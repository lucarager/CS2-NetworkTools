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

    #endregion

    /// <summary>
    /// System responsible for UI Bindings & Lookup Handling.
    /// </summary>
    public partial class NT_TooltipSystem : TooltipSystemBase {
        private NT_NodeSelectionToolSystem m_NodeSelectionToolSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            m_NodeSelectionToolSystem = World.GetOrCreateSystemManaged<NT_NodeSelectionToolSystem>();

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
                    value = $"Node {i}",
                };

                var group = new TooltipGroup {
                    position            = position + 2f,
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
        }
    }
}