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
    using Settings;
    using Unity.Collections;
    using Unity.Entities;

    #endregion

    /// <summary>
    /// Tooltip System.
    /// </summary>
    public partial class NT_ToolTooltipSystem : TooltipSystemBase {
        private InputHintTooltip m_Tooltip_Apply;
        private InputHintTooltip m_Tooltip_SecondaryApply;
        private ToolSystem m_ToolSystem;
        private EntityQuery m_ParcelQuery;

        /// <inheritdoc/>
        protected override void OnCreate() {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

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
            
            //m_Tooltip_Apply.Refresh(InputManager.DeviceType.Mouse);
            //m_Tooltip_SecondaryApply.Refresh(InputManager.DeviceType.Mouse);
            //AddMouseTooltip(m_Tooltip_Apply);
            //AddMouseTooltip(m_Tooltip_SecondaryApply);
        }
    }
}