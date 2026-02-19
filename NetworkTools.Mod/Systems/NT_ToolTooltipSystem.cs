// <copyright file="P_TooltipSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems {
    #region Using Statements

    using System;
    using Game.Input;
    using Game.Prefabs;
    using Game.Tools;
    using Game.UI.Tooltip;
    using NetworkTools.Components;
    using NetworkTools.Settings;
    using NetworkTools.Systems.Tools;
    using Unity.Entities;

    #endregion

    internal enum NT_ToolType {
        None = 0,
        AddNode,
        RemoveNode,
        PathTransform,
        NodeControl
    }

    /// <summary>
    ///     Tooltip System.
    /// </summary>
    public partial class NT_ToolTooltipSystem : TooltipSystemBase {
        private NT_AddNodeToolSystem m_NtAddNodeToolSystem;
        private NT_NodeControlToolSystem m_NtNodeControlToolSystem;
        private NT_PathTransformToolSystem m_NtPathTransformToolSystem;
        private NT_RemoveNodeToolSystem m_NtRemoveNodeToolSystem;
        private EntityQuery m_ParcelQuery;
        protected PrefabSystem m_PrefabSystem;
        private ToolSystem m_ToolSystem;


        /// <inheritdoc />
        protected override void OnCreate() {
            base.OnCreate();

            m_ToolSystem                = World.GetOrCreateSystemManaged<ToolSystem>();
            m_PrefabSystem              = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_NtAddNodeToolSystem       = World.GetOrCreateSystemManaged<NT_AddNodeToolSystem>();
            m_NtRemoveNodeToolSystem    = World.GetOrCreateSystemManaged<NT_RemoveNodeToolSystem>();
            m_NtPathTransformToolSystem = World.GetOrCreateSystemManaged<NT_PathTransformToolSystem>();
            m_NtNodeControlToolSystem   = World.GetOrCreateSystemManaged<NT_NodeControlToolSystem>();
        }

        /// <inheritdoc />
        protected override void OnUpdate() {
            if (m_ToolSystem.activePrefab is not NT_ToolPrefab) {
                return;
            }

            var controlScheme = InputManager.instance.activeControlScheme;

            if (controlScheme is not InputManager.ControlScheme.KeyboardAndMouse) {
                return;
            }

            var activeToolType = NT_ToolType.None;
            var activeTool = default(NT_BaseToolSystem);

            // Check which tool is active
            if (m_ToolSystem.activePrefab is NT_ToolPrefab activePrefab) {
                if (m_PrefabSystem.HasComponent<NT_AddNode>(activePrefab)) {
                    activeToolType = NT_ToolType.AddNode;
                    activeTool     = m_NtAddNodeToolSystem;
                }
                else if (m_PrefabSystem.HasComponent<NT_RemoveNode>(activePrefab)) {
                    activeToolType = NT_ToolType.RemoveNode;
                    activeTool     = m_NtRemoveNodeToolSystem;
                }
                else if (m_PrefabSystem.HasComponent<NT_PathTransform>(activePrefab)) {
                    activeToolType = NT_ToolType.PathTransform;
                    activeTool     = m_NtPathTransformToolSystem;
                }
                else if (m_PrefabSystem.HasComponent<NT_NodeControl>(activePrefab)) {
                    activeToolType = NT_ToolType.NodeControl;
                    activeTool     = m_NtNodeControlToolSystem;
                }
            }

            switch (activeToolType) {
                case NT_ToolType.AddNode:
                    switch (activeTool.Phase) {
                        case OperationPhase.Idle:
                            // Add tooltips
                            var idleTooltip = new StringTooltip {
                                value = "Add Node: Idle"
                            };
                            AddMouseTooltip(idleTooltip);
                            break;
                        case OperationPhase.Configuring:
                            break;
                        case OperationPhase.Ready:
                            break;
                        case OperationPhase.Applying:
                            break;
                    }

                    break;
                case NT_ToolType.RemoveNode:
                    switch (activeTool.Phase) {
                        case OperationPhase.Idle:
                            // Add tooltips
                            var idleTooltip = new StringTooltip {
                                value = "Remove Node: Idle"
                            };
                            AddMouseTooltip(idleTooltip);
                            break;
                        case OperationPhase.Configuring:
                            break;
                        case OperationPhase.Ready:
                            break;
                        case OperationPhase.Applying:
                            break;
                    }

                    break;
                case NT_ToolType.PathTransform:
                    switch (activeTool.Phase) {
                        case OperationPhase.Idle:
                            // Add tooltips
                            var idleTooltip = new StringTooltip {
                                value = "PathTransform: Idle"
                            };
                            AddMouseTooltip(idleTooltip);
                            break;
                        case OperationPhase.Configuring:
                            break;
                        case OperationPhase.Ready:
                            break;
                        case OperationPhase.Applying:
                            break;
                    }

                    break;
                case NT_ToolType.NodeControl:
                    switch (activeTool.Phase) {
                        case OperationPhase.Idle:
                            // Add tooltips
                            var idleTooltip = new StringTooltip {
                                value = "Node Control: Idle"
                            };
                            AddMouseTooltip(idleTooltip);
                            break;
                        case OperationPhase.Configuring:
                            break;
                        case OperationPhase.Ready:
                            break;
                        case OperationPhase.Applying:
                            break;
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}