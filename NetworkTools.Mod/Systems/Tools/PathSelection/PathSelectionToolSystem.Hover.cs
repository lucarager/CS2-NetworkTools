// <copyright file="NT_PathSelectionToolSystem.Hover.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using Game.Tools;
    using NetworkTools.Components;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Partial class containing hover and path preview logic.
    /// </summary>
    public abstract partial class NT_PathSelectionToolSystem {
        /// <summary>
        ///     Updates the preview path based on the current hover target.
        ///     Called when the hovered entity changes.
        /// </summary>
        /// <param name="controlPoint">The control point from raycast.</param>
        protected void HandlePathUpdate(ControlPoint controlPoint) {
            if (CurrentSelectionState == SelectionState.NoSelection) {
                return;
            }

            var startNode = m_SelectedNodes[^1];
            var endNode = controlPoint.m_OriginalEntity;

            // Find path from last selected node to hovered node
            var newPathNodes = new NativeList<Entity>(16, Allocator.Temp);
            var newPathEdges = new NativeList<Entity>(16, Allocator.Temp);
            var newPathFound = FindPathBetween(startNode, endNode, ref newPathNodes, ref newPathEdges);

            if (newPathFound) {
                m_NextPathNodes.Clear();
                m_NextPathNodes.AddRange(newPathNodes.AsArray());
                m_NextPathEdges.Clear();
                m_NextPathEdges.AddRange(newPathEdges.AsArray());

                OnPathPreviewUpdated(endNode);
            }

            newPathNodes.Dispose();
            newPathEdges.Dispose();
        }

        /// <summary>
        ///     Updates highlighting based on new hover state.
        /// </summary>
        /// <param name="hoveredEntity">The entity being hovered.</param>
        protected void HandleHover(Entity hoveredEntity) {
            switch (CurrentSelectionState) {
                case SelectionState.NoSelection:
                    m_Log.Debug("[NoSelection] Hovering over potential start point.");
                    SwapHighlightedEntities(m_LastHoveredEntity.Value, hoveredEntity, NT_Highlighted.DefaultNode);
                    break;
                case SelectionState.StartNodeSelected:
                    PreviewPath();
                    m_Log.Debug("[StartNodeSelected] Hovering over potential end point.");
                    break;
                case SelectionState.EndNodeSelected:
                    PreviewPath();
                    m_Log.Debug("[EndNodeSelected] Hovering over another potential end point.");
                    break;
            }
        }

        /// <summary>
        ///     Handles the case when hovering over nothing (no raycast hit).
        ///     Clears preview state and highlights.
        /// </summary>
        protected void HandleNoHover() {
            m_NextPathNodes.Clear();
            m_NextPathEdges.Clear();
            m_LastHoveredEntity.Value = Entity.Null;
            m_SelectionLastHitPosition = float3.zero;
            ClearAllHighlights();
        }

        /// <summary>
        ///     Updates highlighting for hovered path.
        ///     Highlights both nodes and edges in the path from the last selected node to the hovered node.
        /// </summary>
        protected void PreviewPath() {
            // Clear any existing highlights
            ClearAllHighlights();

            // Add highlights to nodes
            foreach (var node in m_NextPathNodes) {
                AddHighlight(node, NT_Highlighted.DefaultNode);
            }

            // Add highlights to edges
            foreach (var edge in m_NextPathEdges) {
                AddHighlight(edge, NT_Highlighted.DefaultEdge);
            }
        }
    }
}
