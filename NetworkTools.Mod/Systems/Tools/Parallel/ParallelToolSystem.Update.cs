// <copyright file="ParallelToolSystem.Update.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools.Parallel {
    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Tools;
    using NetworkTools.Components;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    ///     Update logic for the Parallel tool.
    /// </summary>
    public partial class NT_ParallelToolSystem {
        /// <inheritdoc />
        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            UpdateActions();

            // ═══════════════════════════════════════════════════════════════════════════
            // HANDLE INTERACTION PIPELINE 
            // ═══════════════════════════════════════════════════════════════════════════

            if (Phase == OperationPhase.Ready && ProcessHandleInput(inputDeps)) {
                // Handle consumed input (e.g., adjusting offset)
                return HandleTempEntities(inputDeps);
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // NODE SELECTION: Input Detection 
            // ═══════════════════════════════════════════════════════════════════════════

            var rightClickPressed = m_SecondaryApplyAction.WasPressedThisFrame();
            var leftClickPressed = m_ApplyAction.WasPressedThisFrame();
            var raycastHit    = GetRaycastResult(out var controlPoint);
            var hoveredEntity = raycastHit ? controlPoint.m_OriginalEntity : Entity.Null;

            // ═══════════════════════════════════════════════════════════════════════════
            // NODE SELECTION: State Mutation
            // Phase is automatically updated by HandleAddNode/HandleRemoveNode
            // ═══════════════════════════════════════════════════════════════════════════

            if (rightClickPressed) {
                HandleRemoveNode();
                m_UpdateNeeded = true;
            }
            else if (raycastHit) {
                // Update hover state
                if (hoveredEntity != m_LastHoveredEntity.Value) {
                    HandlePathUpdate(controlPoint);
                    HandleHover(hoveredEntity);
                    m_UpdateNeeded = true;
                }
                m_LastHoveredEntity.Value = hoveredEntity;

                // Left-click: add node
                if (leftClickPressed && hoveredEntity != Entity.Null) {
                    HandleAddNode(hoveredEntity);
                    m_UpdateNeeded = true;
                }
            }
            else {
                HandleNoHover();
            }

            // ═══════════════════════════════════════════════════════════════════════════
            // OUTPUT
            // ═══════════════════════════════════════════════════════════════════════════

            return HandleTempEntities(inputDeps);
        }

        /// <summary>
        ///     Runs various jobs depending on whether we need to Update, Apply, or Cancel temp entities.
        /// </summary>
        /// <param name="inputDeps">Input job dependencies.</param>
        /// <returns>Output job handle.</returns>
        private JobHandle HandleTempEntities(JobHandle inputDeps) {
            return Phase switch {
                // Preview temp entities
                OperationPhase.Ready => Update(inputDeps),
                // Apply real entities
                OperationPhase.Applying => Apply(inputDeps),
                // Clear otherwise
                OperationPhase.Idle or OperationPhase.Configuring => Clear(inputDeps),
                _ => Clear(inputDeps)
            };
        }

        /// <inheritdoc />
        public int ApplyMinNodeCount => 2;

        /// <inheritdoc />
        public bool CanApply => Phase == OperationPhase.Ready;

        /// <summary>
        ///     Requests the tool to apply the current transformation.
        /// </summary>
        public void RequestApply() {
            if (Phase != OperationPhase.Ready) {
                return;
            }

            Phase = OperationPhase.Applying;
            m_UpdateNeeded = true;
        }

        /// <inheritdoc />
        protected override bool GetRaycastResult(out ControlPoint controlPoint) =>
            TryGetNodeRaycast(out controlPoint);

        /// <summary>
        ///     Resets the tool to idle state.
        /// </summary>
        public void ResetToIdle() {
            Phase = OperationPhase.Idle;
            DestroyAllHandles();
            ClearSelectionState();
        }
    }
}
