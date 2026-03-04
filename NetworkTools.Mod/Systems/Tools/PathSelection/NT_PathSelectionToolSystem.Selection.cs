// <copyright file="NT_PathSelectionToolSystem.Selection.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using NetworkTools.Components;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    ///     Partial class containing node selection logic.
    /// </summary>
    public abstract partial class NT_PathSelectionToolSystem {
        #region Phase Management

        /// <summary>
        ///     Updates the OperationPhase based on the current selection state.
        ///     Called automatically by HandleAddNode/HandleRemoveNode.
        /// </summary>
        /// <returns>The previous phase before the update.</returns>
        private OperationPhase UpdatePhaseFromSelection() {
            // Don't interrupt an active apply operation
            if (Phase == OperationPhase.Applying) {
                return Phase;
            }

            var previousPhase = Phase;

            Phase = CurrentSelectionState switch {
                SelectionState.NoSelection => OperationPhase.Idle,
                SelectionState.StartNodeSelected => OperationPhase.Configuring,
                _ => OperationPhase.Ready
            };

            return previousPhase;
        }

        #endregion

        #region Node Add/Remove

        /// <summary>
        ///     Attempts to add a node to the path. Returns true if selection changed.
        ///     Automatically updates Phase and invokes appropriate template methods.
        /// </summary>
        /// <param name="entity">The node entity to add.</param>
        /// <returns>True if the node was added and selection changed.</returns>
        protected bool HandleAddNode(Entity entity) {
            if (entity == Entity.Null || m_SelectedNodes.Contains(entity)) {
                return false;
            }

            m_Log.Debug($"[{CurrentSelectionState}] Adding node: {entity}");

            // Add node to selection and mark with state-specific components
            switch (CurrentSelectionState) {
                case SelectionState.NoSelection:
                    m_Log.Debug("→ StartNodeSelected");
                    m_SelectedNodes.Add(entity);
                    EntityManager.AddComponentData(entity, NT_Selected.DefaultNode);
                    EntityManager.AddComponent<NT_SelectedFirst>(entity);
                    break;

                case SelectionState.StartNodeSelected:
                    m_Log.Debug("→ EndNodeSelected");
                    m_SelectedNodes.Add(entity);
                    EntityManager.AddComponentData(entity, NT_Selected.DefaultNode);
                    EntityManager.AddComponent<NT_SelectedLast>(entity);
                    break;

                case SelectionState.EndNodeSelected:
                    m_Log.Debug("→ Extending path");

                    // Remove SelectedLast from old endpoint
                    var previousEnd = m_SelectedNodes[^1];
                    EntityManager.RemoveComponent<NT_SelectedLast>(previousEnd);

                    // Add new endpoint
                    m_SelectedNodes.Add(entity);
                    EntityManager.AddComponentData(entity, NT_Selected.DefaultNode);
                    EntityManager.AddComponent<NT_SelectedLast>(entity);
                    break;
            }

            // Merge preview path into persistent path
            CommitNextPathToCurrentPath();

            // Update path indices for rendering
            UpdatePathIndices();

            // Recalculate eligible nodes from new endpoint
            RecalculateEligibleNodes(entity);

            // Update phase and invoke template methods
            var previousPhase = UpdatePhaseFromSelection();
            if (Phase == OperationPhase.Ready && previousPhase != OperationPhase.Ready) {
                // Entered Ready state (first complete path)
                OnPathReady();
            } else if (Phase == OperationPhase.Ready && previousPhase == OperationPhase.Ready) {
                // Already in Ready, path was extended
                OnPathExtended(entity);
            }

            return true;
        }

        /// <summary>
        ///     Removes the last node from the path. Returns true if selection changed.
        ///     Automatically updates Phase and invokes appropriate template methods.
        /// </summary>
        /// <returns>True if a node was removed and selection changed.</returns>
        protected bool HandleRemoveNode() {
            var lastNode = m_SelectedNodes.Length == 0 ? m_SelectedNodes[^1] : Entity.Null;

            m_Log.Debug($"[{CurrentSelectionState}] Removing node: {lastNode}");

            switch (CurrentSelectionState) {
                case SelectionState.NoSelection:
                    m_Log.Debug("Cancel pressed, exiting tool.");
                    RequestDisable();
                    break;

                case SelectionState.StartNodeSelected:
                    m_Log.Debug("→ NoSelection");
                    EntityManager.RemoveComponent<NT_Selected>(lastNode);
                    EntityManager.RemoveComponent<NT_SelectedFirst>(lastNode);
                    m_SelectedNodes.RemoveAt(m_SelectedNodes.Length - 1);
                    ResetToNoSelection();
                    UpdatePhaseFromSelection();
                    OnSelectionCleared();
                    return true;

                case SelectionState.EndNodeSelected:
                    m_Log.Debug("→ Trimming path");

                    // Remove endpoint marker from current last node
                    EntityManager.RemoveComponent<NT_Selected>(lastNode);
                    EntityManager.RemoveComponent<NT_SelectedLast>(lastNode);
                    m_SelectedNodes.RemoveAt(m_SelectedNodes.Length - 1);

                    // Trim path nodes/edges back to the new endpoint
                    var newEndNode = m_SelectedNodes[^1];
                    TrimPathToNode(newEndNode);

                    // Update rendering indices
                    UpdatePathIndices();

                    // Recalculate eligible nodes from new endpoint
                    RecalculateEligibleNodes(newEndNode);

                    // Update phase and invoke template methods
                    UpdatePhaseFromSelection();

                    if (m_SelectedNodes.Length >= 2) {
                        EntityManager.AddComponent<NT_SelectedLast>(newEndNode);
                        OnPathTrimmed(newEndNode);
                    } else {
                        // Down to just start node - path no longer complete
                        OnSelectionCleared();
                    }

                    return true;
            }

            return false;
        }

        #endregion

        #region Path Management

        /// <summary>
        ///     Merges the hovered preview path (m_NextPathNodes/Edges) into the committed path.
        ///     Called when a node is added — this confirms the hover preview.
        /// </summary>
        private void CommitNextPathToCurrentPath() {
            // Add new path nodes
            foreach (var node in m_NextPathNodes) {
                if (!m_CurrentPathNodes.Contains(node)) {
                    m_CurrentPathNodes.Add(node);
                    EntityManager.AddComponentData(node, NT_Selected.DefaultNode);
                }
            }

            // Add new path edges
            foreach (var edge in m_NextPathEdges) {
                if (!m_CurrentPathEdges.Contains(edge)) {
                    m_CurrentPathEdges.Add(edge);
                    EntityManager.AddComponentData(edge, NT_Selected.DefaultEdge);
                }
            }

            // Clear preview (will be rebuilt on next hover)
            m_NextPathNodes.Clear();
            m_NextPathEdges.Clear();

            m_Log.Debug($"CommitNextPathToCurrentPath: Path now {m_CurrentPathNodes.Length} nodes, {m_CurrentPathEdges.Length} edges");
        }

        /// <summary>
        ///     Recalculates which nodes are eligible for selection from the current endpoint.
        ///     Replaces NT_Eligible on all nodes with only those reachable from the endpoint.
        /// </summary>
        /// <param name="fromNode">The node to calculate eligibility from.</param>
        private void RecalculateEligibleNodes(Entity fromNode) {
            // Clear all NT_Eligible
            EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);

            // Find new eligible nodes
            m_EligibleNodes.Clear();
            FindEligibleNodes(fromNode, m_EligibleNodes);

            // Add NT_Eligible to newly eligible nodes
            var eligibleArray = new NativeArray<Entity>(m_EligibleNodes.AsArray(), Allocator.Temp);
            EntityManager.AddComponent<NT_Eligible>(eligibleArray);

            m_Log.Debug($"RecalculateEligibleNodes: {m_EligibleNodes.Length} nodes reachable from endpoint");
        }

        /// <summary>
        ///     Updates the PathIndex for all nodes in m_CurrentPathNodes to reflect their position in the path.
        ///     This should be called after any add/remove operation to keep indices synchronized.
        /// </summary>
        private void UpdatePathIndices() {
            for (var i = 0; i < m_CurrentPathNodes.Length; i++) {
                var node = m_CurrentPathNodes[i];
                if (EntityManager.HasComponent<NT_Selected>(node)) {
                    EntityManager.SetComponentData(node, NT_Selected.ForNode(NodeRenderMode.RenderAsCircle, i));
                }
            }
        }

        /// <summary>
        ///     Removes path nodes and edges from the end back to (and including) targetNode.
        ///     Used when backing up the path via right-click.
        /// </summary>
        /// <param name="targetNode">The node to trim back to.</param>
        private void TrimPathToNode(Entity targetNode) {
            var trimmedNodes = 0;
            var trimmedEdges = 0;

            var done = false;
            while (!done) {
                // Stop if we've reached the target or emptied the path
                if (m_CurrentPathNodes.Length == 0 || m_CurrentPathNodes[^1] == targetNode) {
                    done = true;
                    break;
                }

                // Remove trailing node
                var nodeToRemove = m_CurrentPathNodes[^1];
                EntityManager.RemoveComponent<NT_Selected>(nodeToRemove);
                m_CurrentPathNodes.RemoveAt(m_CurrentPathNodes.Length - 1);
                trimmedNodes++;

                // Remove trailing edge
                if (m_CurrentPathEdges.Length > 0) {
                    var edgeToRemove = m_CurrentPathEdges[^1];
                    EntityManager.RemoveComponent<NT_Selected>(edgeToRemove);
                    m_CurrentPathEdges.RemoveAt(m_CurrentPathEdges.Length - 1);
                    trimmedEdges++;
                }
            }

            m_Log.Debug($"TrimPathToNode: Removed {trimmedNodes} nodes, {trimmedEdges} edges");
        }

        #endregion

        #region State Reset

        /// <summary>
        ///     Resets to NoSelection state. Makes all nodes eligible.
        ///     Called when starting the tool or clearing selection.
        /// </summary>
        protected void ResetToNoSelection() {
            m_Log.Debug("ResetToNoSelection()");

            // Clear caches
            m_SelectedNodes.Clear();
            m_EligibleNodes.Clear();
            m_CurrentPathNodes.Clear();
            m_CurrentPathEdges.Clear();

            // Add NT_Eligible to ALL nodes 
            EntityManager.AddComponent<NT_Eligible>(m_NodesWithoutEligibleQuery);
        }

        /// <summary>
        ///     Clears all selection state and resets to idle.
        ///     Called when tool stops or user explicitly resets.
        /// </summary>
        protected void ClearSelectionState() {
            // Batch remove all marker components using cached queries
            EntityManager.RemoveComponent<NT_Selected>(m_NodesWithSelectedQuery);
            EntityManager.RemoveComponent<NT_Selected>(m_EdgesWithSelectedQuery);
            EntityManager.RemoveComponent<NT_Eligible>(m_NodesWithEligibleQuery);
            EntityManager.RemoveComponent<NT_SelectedFirst>(m_NodesWithSelectedFirstQuery);
            EntityManager.RemoveComponent<NT_SelectedLast>(m_NodesWithSelectedLastQuery);

            ClearAllHighlights();

            // Reset state
            ResetToNoSelection();
        }

        #endregion
    }
}
