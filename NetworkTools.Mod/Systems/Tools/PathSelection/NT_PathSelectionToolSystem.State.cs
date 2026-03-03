// <copyright file="NT_PathSelectionToolSystem.State.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Partial class containing selection state fields and properties.
    /// </summary>
    public abstract partial class NT_PathSelectionToolSystem {
        #region Selection State

        /// <summary>
        ///     List of user-selected node entities that define path endpoints.
        ///     These are the nodes the user explicitly clicked on.
        /// </summary>
        protected NativeList<Entity> m_SelectedNodes;

        /// <summary>
        ///     Currently committed path of nodes (includes all nodes between selected endpoints).
        /// </summary>
        protected NativeList<Entity> m_CurrentPathNodes;

        /// <summary>
        ///     Currently committed path of edges (includes all edges between selected endpoints).
        /// </summary>
        protected NativeList<Entity> m_CurrentPathEdges;

        /// <summary>
        ///     Preview path of nodes (updated on hover, before user commits).
        /// </summary>
        protected NativeList<Entity> m_NextPathNodes;

        /// <summary>
        ///     Preview path of edges (updated on hover, before user commits).
        /// </summary>
        protected NativeList<Entity> m_NextPathEdges;

        /// <summary>
        ///     List of nodes currently eligible for selection (reachable from current endpoint).
        /// </summary>
        protected NativeList<Entity> m_EligibleNodes;

        /// <summary>
        ///     Caches the last raycast hit position.
        /// </summary>
        protected float3 m_SelectionLastHitPosition;

        #endregion

        #region Public Properties

        /// <summary>
        ///     Gets the current selection state based on the number of selected nodes.
        /// </summary>
        public SelectionState CurrentSelectionState =>
            m_SelectedNodes.Length switch {
                0 => SelectionState.NoSelection,
                1 => SelectionState.StartNodeSelected,
                _ => SelectionState.EndNodeSelected
            };

        /// <summary>
        ///     Gets a value indicating whether a complete path is selected (2+ nodes).
        /// </summary>
        public bool HasCompletePath => m_SelectedNodes.Length >= 2;

        /// <summary>
        ///     Gets the start node of the selection, or Entity.Null if none selected.
        /// </summary>
        public Entity StartNode => m_SelectedNodes.Length > 0 ? m_SelectedNodes[0] : Entity.Null;

        /// <summary>
        ///     Gets the end node of the selection, or Entity.Null if less than 2 nodes selected.
        /// </summary>
        public Entity EndNode => m_SelectedNodes.Length >= 2 ? m_SelectedNodes[^1] : Entity.Null;

        /// <summary>
        ///     Gets the number of nodes in the current path (including intermediate nodes).
        /// </summary>
        public int PathNodeCount => m_CurrentPathNodes.Length;

        /// <summary>
        ///     Gets the number of edges in the current path.
        /// </summary>
        public int PathEdgeCount => m_CurrentPathEdges.Length;

        #endregion

        #region Public Methods

        /// <summary>
        ///     Gets the array of user-selected node entities (path endpoints).
        /// </summary>
        /// <returns>Array of selected Entity objects.</returns>
        public Entity[] GetSelectedNodes() {
            return m_SelectedNodes.ToArray(Allocator.Temp).ToArray();
        }

        /// <summary>
        ///     Gets the array of all nodes in the current path.
        /// </summary>
        /// <returns>Array of Entity objects in path order.</returns>
        public Entity[] GetPathNodes() {
            return m_CurrentPathNodes.ToArray(Allocator.Temp).ToArray();
        }

        /// <summary>
        ///     Gets the array of all edges in the current path.
        /// </summary>
        /// <returns>Array of Entity objects in path order.</returns>
        public Entity[] GetPathEdges() {
            return m_CurrentPathEdges.ToArray(Allocator.Temp).ToArray();
        }

        #endregion
    }
}
