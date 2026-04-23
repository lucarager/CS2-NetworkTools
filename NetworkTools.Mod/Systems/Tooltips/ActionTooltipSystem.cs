// <copyright file="NT_ActionTooltipSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tooltips {
    using Game.Input;
    using Game.Tools;
    using Game.UI.Tooltip;
    using NetworkTools.Systems.Tools;

    /// <summary>
    ///     Tooltip System.
    ///     Queries the active tool for hint tooltips and displays them.
    /// </summary>
    public partial class NT_ActionTooltipSystem : TooltipSystemBase {
        private ToolSystem  m_ToolSystem;
        private ProxyAction m_ApplyAction;
        private ProxyAction m_SecondaryApplyAction;

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            var inputManager = InputManager.instance;
            if (inputManager != null) {
                m_ApplyAction          = inputManager.FindAction(InputManager.kToolMap, "Apply");
                m_SecondaryApplyAction = inputManager.FindAction(InputManager.kToolMap, "Secondary Apply");
            }
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            if (m_ToolSystem.activeTool is not NT_BaseToolSystem activeTool) {
                return;
            }

            var controlScheme = InputManager.instance.activeControlScheme;
            if (controlScheme is not InputManager.ControlScheme.KeyboardAndMouse) {
                return;
            }

            var tooltipEntries = activeTool.GetHintTooltips(
                activeTool.Phase,
                m_ApplyAction,
                m_SecondaryApplyAction);

            ShowTooltips(tooltipEntries, InputManager.DeviceType.Mouse);
        }

        private void ShowTooltips(System.Collections.Generic.IReadOnlyList<HintTooltipEntry> entries,
                                  InputManager.DeviceType                                    device) {
            if (entries == null || entries.Count == 0) {
                return;
            }

            for (var i = 0; i < entries.Count; i++) {
                var entry = entries[i];

                if (entry.Action != null) {
                    var displayOverride = new DisplayNameOverride(
                        "NetworkTools.HintTooltip.Tooltip",
                        entry.Action,
                        entry.Text,
                        0,
                        InputManager.DeviceType.All,
                        UIBaseInputAction.Transform.None
                    );
                    displayOverride.active = true;

                    var inputHint = new InputHintTooltip(entry.Action);
                    inputHint.Refresh(device);

                    displayOverride.Dispose();
                } else {
                    AddMouseTooltip(new StringTooltip { value = entry.Text });
                }
            }
        }
    }
}