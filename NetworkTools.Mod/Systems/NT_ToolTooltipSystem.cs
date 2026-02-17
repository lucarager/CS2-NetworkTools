// <copyright file="P_TooltipSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using System.Collections.Generic;
    using Game.Common;
    using Game.Input;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI.Localization;
    using Game.UI.Tooltip;
    using NetworkTools.Components;
    using Settings;
    using Unity.Collections;
    using Unity.Entities;
    using static Colossal.AssetPipeline.Diagnostic.Report;

    #endregion

    enum NT_ToolType {
        None = 0,
        AddNode,
        RemoveNode,
        PathTransform,
        NodeControl,
    }

    /// <summary>
    /// Tooltip System.
    /// </summary>
    public partial class NT_ToolTooltipSystem : TooltipSystemBase {
        private InputHintTooltip m_Tooltip_Apply;
        private InputHintTooltip m_Tooltip_SecondaryApply;
        private ToolSystem m_ToolSystem;
        private EntityQuery m_ParcelQuery;
        protected PrefabSystem m_PrefabSystem;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            m_Tooltip_Apply = new InputHintTooltip(InputManager.instance.FindAction("NetworkTools.NetworkTools.NetworkToolsMod", NT_Settings.ApplyActionStr));
            m_Tooltip_SecondaryApply = new InputHintTooltip(InputManager.instance.FindAction("NetworkTools.NetworkTools.NetworkToolsMod", NT_Settings.SecondaryApplyActionStr));
        }

        /// <inheritdoc/>
        protected override void OnUpdate() {
            if (m_ToolSystem.activePrefab is not NT_ToolPrefab) {
                return;
            }

            var controlScheme = InputManager.instance.activeControlScheme;

            if (controlScheme is not InputManager.ControlScheme.KeyboardAndMouse) {
                return;
            }
            
            var activeToolType = NT_ToolType.None;

            // Check which tool is active
            if (m_ToolSystem.activePrefab is NT_ToolPrefab activePrefab) {
                if (m_PrefabSystem.HasComponent<NT_AddNode>(activePrefab)) {
                    activeToolType = NT_ToolType.AddNode;
                }
                else if (m_PrefabSystem.HasComponent<NT_RemoveNode>(activePrefab)) {
                    activeToolType = NT_ToolType.RemoveNode;
                }
                else if (m_PrefabSystem.HasComponent<NT_PathTransform>(activePrefab)) {
                    activeToolType = NT_ToolType.PathTransform;
                }
                else if (m_PrefabSystem.HasComponent<NT_NodeControl>(activePrefab)) {
                    activeToolType = NT_ToolType.NodeControl;
                }
            }

            switch (activeToolType) {
                case NT_ToolType.AddNode:
                    // Process state
                    // Add tooltips
                    //var mouseTooltip = new StringTooltip() {
                    //    value = "Add Helper",
                    //};
                    //AddMouseTooltip(mouseTooltip);
                    break;
            }
        }
    }
}