// <copyright file="RemoveNodeToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using Game.Common;
    using Game.Input;
    using Game.Net;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    /// <summary>
    ///     Remove Node tool — merges two edges by removing the intermediate node.
    /// </summary>
    public partial class NT_RemoveNodeToolSystem : NT_BaseToolSystem, IToolPrefabProvider {
        public override string toolID => "RemoveNode Tool";

        protected override JobHandle OnUpdate(JobHandle inputDeps) {
            UpdateActions();

            // Right click => Cancel / Deselect
            if (m_SecondaryApplyAction.WasPressedThisFrame())
            {
                CancelHandleInteraction();
                HandleCancel();
                return inputDeps;
            }

            // Get raycast result
            if (GetRaycastResult(out var controlPoint)) {
                // We hit something
                var newEntityWasHit = m_LastHoveredEntity.Value != controlPoint.m_OriginalEntity;
                m_UpdateNeeded = newEntityWasHit;

                if (newEntityWasHit) {
                    HandleHover(controlPoint);
                }

                // Update Cache
                m_LastHoveredEntity.Value = controlPoint.m_OriginalEntity;

                // Handle clicking
                if (m_ApplyAction.WasPressedThisFrame()) {
                    HandleApply();
                }
            }
            else {
                // No entity under cursor
                HandleNoHover();
            }

            // Handle temp entities
            return HandleTempEntities(inputDeps);
        }

        private void HandleCancel() {
            m_Log.Debug("Cancel pressed, exiting tool.");
            RequestDisable();
        }

        private void HandleApply() {
            Phase = OperationPhase.Applying;
        }

        /// <summary>
        /// Runs various jobs depending on whether we need to Update, Apply, or Cancel temp entities.
        /// For the remove node tool, we show preview when hovering over a valid node.
        /// </summary>
        /// <param name="inputDeps"></param>
        /// <param name="updateNeeded"></param>
        /// <returns>inputDeps</returns>
        private JobHandle HandleTempEntities(JobHandle inputDeps) {
            return Phase switch {
                OperationPhase.Ready =>
                    // Show preview when hovering over a valid (eligible) node
                    Update(inputDeps),
                OperationPhase.Applying =>
                    // Apply removal
                    Apply(inputDeps),
                OperationPhase.Idle =>
                    // Clear preview when not hovering
                    Clear(inputDeps),
                _ => Clear(inputDeps)
            };
        }

        private void HandleNoHover() {
            m_LastHoveredEntity.Value = Entity.Null;
            Phase    = OperationPhase.Idle;
            ClearAllHighlights();
        }

        private void HandleHover(ControlPoint controlPoint) {
            Phase = OperationPhase.Ready;
            SwapHighlightedEntities(m_LastHoveredEntity.Value, controlPoint.m_OriginalEntity, Components.NT_Highlighted.DefaultNode);
        }

        protected override bool GetRaycastResult(out ControlPoint controlPoint) =>
            TryGetNodeRaycast(out controlPoint);
    }
}
