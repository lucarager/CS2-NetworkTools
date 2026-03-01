// <copyright file="P_TooltipSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    using System.Collections.Generic;
    using Game.Input;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI.Tooltip;
    using NetworkTools.Components;
    using NetworkTools.Systems.Tools;
    using NetworkTools.Systems.Tools.RoadShape;
    using Unity.Entities;

    internal enum NT_ToolType {
        None = 0,
        AddNode,
        RemoveNode,
        PathTransform,
        NodeControl
    }

    /// <summary>
    /// Configuration for a single tooltip entry.
    /// Wraps text in Common.ACTION[] format when no action is provided.
    /// </summary>
    internal record TooltipEntry {
        public string Text { get; }
        public ProxyAction Action { get; }

        public TooltipEntry(string text, ProxyAction action = null) {
            Action = action;
            Text = action == null ? $"Common.ACTION[{text}]" : text;
        }
    }

    /// <summary>
    ///     Tooltip System.
    /// </summary>
    public partial class NT_HintTooltipSystem : TooltipSystemBase {
        private NT_AddNodeToolSystem m_NtAddNodeToolSystem;
        private NT_NodeControlToolSystem m_NtNodeControlToolSystem;
        private NT_RoadShapeToolSystem m_NtRoadShapeToolSystem;
        private NT_RemoveNodeToolSystem m_NtRemoveNodeToolSystem;
        protected PrefabSystem m_PrefabSystem;
        private ToolSystem m_ToolSystem;
        private Dictionary<(NT_ToolType, OperationPhase), List<TooltipEntry>> m_TooltipConfig;
        private ProxyAction m_ApplyAction;
        private ProxyAction m_SecondaryApplyAction;

        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            m_ToolSystem                = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PrefabSystem              = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_NtAddNodeToolSystem       = World.GetOrCreateSystemManaged<NT_AddNodeToolSystem>();
            m_NtRemoveNodeToolSystem    = World.GetOrCreateSystemManaged<NT_RemoveNodeToolSystem>();
            m_NtRoadShapeToolSystem = World.GetOrCreateSystemManaged<NT_RoadShapeToolSystem>();
            m_NtNodeControlToolSystem   = World.GetOrCreateSystemManaged<NT_NodeControlToolSystem>();

            InitializeTooltipConfig();
        }

        protected override void OnDestroy() {
            base.OnDestroy();
            m_TooltipConfig = null;
        }

        private void InitializeTooltipConfig() {
            var inputManager = InputManager.instance;
            if (inputManager == null) {
                return;
            }

            m_ApplyAction = inputManager.FindAction(InputManager.kToolMap, "Apply");
            m_SecondaryApplyAction = inputManager.FindAction(InputManager.kToolMap, "Secondary Apply");

            m_TooltipConfig = new Dictionary<(NT_ToolType, OperationPhase), List<TooltipEntry>> {
                // AddNode Tool
                [(NT_ToolType.AddNode, OperationPhase.Idle)] = new List<TooltipEntry> {
                    new("NetworkTools.AddNode.Hover"),
                    new("NetworkTools.AddNode.Cancel", m_SecondaryApplyAction)
                },
                [(NT_ToolType.AddNode, OperationPhase.Configuring)] = new List<TooltipEntry> {
                    new("NetworkTools.AddNode.Apply", m_ApplyAction),
                    new("NetworkTools.AddNode.Cancel", m_SecondaryApplyAction)
                },
                [(NT_ToolType.AddNode, OperationPhase.Applying)] = new List<TooltipEntry> {
                },

                // RemoveNode Tool
                [(NT_ToolType.RemoveNode, OperationPhase.Idle)] = new List<TooltipEntry> {
                    new("NetworkTools.RemoveNode.Select"),
                    new("NetworkTools.RemoveNode.Cancel", m_SecondaryApplyAction)
                },
                [(NT_ToolType.RemoveNode, OperationPhase.Ready)] = new List<TooltipEntry> {
                    new("NetworkTools.RemoveNode.Apply", m_ApplyAction),
                    new("NetworkTools.RemoveNode.Cancel", m_SecondaryApplyAction)
                },

                // PathTransform Tool
                [(NT_ToolType.PathTransform, OperationPhase.Idle)] = new List<TooltipEntry> {
                    new("NetworkTools.PathTransform.SelectStart", m_ApplyAction)
                },
                [(NT_ToolType.PathTransform, OperationPhase.Configuring)] = new List<TooltipEntry> {
                    new("NetworkTools.PathTransform.SelectSecond", m_ApplyAction),
                    new("NetworkTools.PathTransform.RemoveLast", m_SecondaryApplyAction)
                },
                [(NT_ToolType.PathTransform, OperationPhase.Ready)] = new List<TooltipEntry> {
                    new("NetworkTools.PathTransform.ExtendPath", m_ApplyAction),
                    new("NetworkTools.PathTransform.RemoveLast", m_SecondaryApplyAction)
                },

                // NodeControl Tool
                [(NT_ToolType.NodeControl, OperationPhase.Idle)] = new List<TooltipEntry> {
                    new("NetworkTools.NodeControl.Select", m_ApplyAction)
                },
                [(NT_ToolType.NodeControl, OperationPhase.Ready)] = new List<TooltipEntry> {
                    new("NetworkTools.NodeControl.Adjust", m_ApplyAction),
                    new("NetworkTools.NodeControl.Cancel", m_SecondaryApplyAction)
                }
            };
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            if (m_ToolSystem.activePrefab is not NT_ToolPrefab activePrefab) {
                return;
            }

            var controlScheme = InputManager.instance.activeControlScheme;
            if (controlScheme is not InputManager.ControlScheme.KeyboardAndMouse) {
                return;
            }

            var (toolType, tool) = GetActiveToolInfo(activePrefab);
            if (toolType == NT_ToolType.None || tool == null) {
                return;
            }

            ShowTooltipsForTool(toolType, tool.Phase, InputManager.DeviceType.Mouse);
        }

        private (NT_ToolType, NT_BaseToolSystem) GetActiveToolInfo(NT_ToolPrefab prefab) {
            if (m_PrefabSystem.HasComponent<NT_AddNode>(prefab)) {
                return (NT_ToolType.AddNode, m_NtAddNodeToolSystem);
            }
            if (m_PrefabSystem.HasComponent<NT_RemoveNode>(prefab)) {
                return (NT_ToolType.RemoveNode, m_NtRemoveNodeToolSystem);
            }
            if (m_PrefabSystem.HasComponent<NT_PathTransform>(prefab)) {
                return (NT_ToolType.PathTransform, m_NtRoadShapeToolSystem);
            }
            if (m_PrefabSystem.HasComponent<NT_NodeControl>(prefab)) {
                return (NT_ToolType.NodeControl, m_NtNodeControlToolSystem);
            }
            return (NT_ToolType.None, null);
        }

        private void ShowTooltipsForTool(NT_ToolType toolType, OperationPhase phase, InputManager.DeviceType device) {
            if (m_TooltipConfig == null || !m_TooltipConfig.TryGetValue((toolType, phase), out var tooltipEntries)) {
                return;
            }

            foreach (var entry in tooltipEntries) {
                if (entry.Action != null) {
                    var displayOverride = new DisplayNameOverride(
                        "NetworkTools.Tooltip",
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