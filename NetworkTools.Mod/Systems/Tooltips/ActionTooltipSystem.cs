// <copyright file="NT_ActionTooltipSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tooltips {
    using System.Collections.Generic;

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
        private ProxyAction m_PreciseRotation;

        private readonly Dictionary<ProxyAction, DisplayNameOverride> m_Overrides = new();

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            var inputManager = InputManager.instance;
            if (inputManager != null) {
                m_ApplyAction          = inputManager.FindAction(InputManager.kToolMap, "Apply");
                m_SecondaryApplyAction = inputManager.FindAction(InputManager.kToolMap, "Secondary Apply");
                m_PreciseRotation      = inputManager.FindAction(InputManager.kToolMap, "Precise Rotation");
            }
        }

        /// <inheritdoc />
        protected override void OnDestroy() {
            foreach (var kvp in m_Overrides) {
                kvp.Value.Dispose();
            }

            m_Overrides.Clear();
            base.OnDestroy();
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            if (m_ToolSystem.activeTool is not NT_BaseToolSystem activeTool) {
                DeactivateOverrides();
                return;
            }

            var controlScheme = InputManager.instance.activeControlScheme;
            if (controlScheme is not InputManager.ControlScheme.KeyboardAndMouse) {
                DeactivateOverrides();
                return;
            }

            var tooltipEntries = activeTool.GetHintTooltips(
                activeTool.Phase,
                m_ApplyAction,
                m_SecondaryApplyAction,
                m_PreciseRotation);

            ShowTooltips(tooltipEntries, InputManager.DeviceType.Mouse);
        }

        private void ShowTooltips(IReadOnlyList<HintTooltipEntry> entries,
                                  InputManager.DeviceType         device) {
            var activeActions = new HashSet<ProxyAction>();

            if (entries != null && entries.Count > 0) {
                for (var i = 0; i < entries.Count; i++) {
                    var entry = entries[i];

                    if (entry.Action != null) {
                        activeActions.Add(entry.Action);

                        if (!m_Overrides.TryGetValue(entry.Action, out var dno)) {
                            dno = new DisplayNameOverride(
                                "NetworkTools.HintTooltip.Tooltip",
                                entry.Action,
                                entry.Text,
                                1);
                            m_Overrides[entry.Action] = dno;
                        }

                        dno.displayName = entry.Text;
                        dno.active      = true;

                        var inputHint = new InputHintTooltip(entry.Action);
                        inputHint.Refresh(device);

                        if (inputHint.path != "Tool/Secondary ApplyMouse" &&
                            inputHint.path != "Tool/ApplyMouse") {
                            AddMouseTooltip(inputHint);
                        }
                    } else {
                        AddMouseTooltip(new StringTooltip { value = entry.Text });
                    }
                }
            }

            foreach (var kvp in m_Overrides) {
                if (!activeActions.Contains(kvp.Key) && kvp.Value.active) {
                    kvp.Value.active = false;
                }
            }
        }

        private void DeactivateOverrides() {
            foreach (var kvp in m_Overrides) {
                if (kvp.Value.active) {
                    kvp.Value.active = false;
                }
            }
        }
    }
}