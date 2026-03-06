// <copyright file="NT_PathSelectionToolSystem.cs" company="Luca Rager">
// Copyright (c) Luca Rager. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace NetworkTools.Systems.Tools {
    using Unity.Entities;

    /// <summary>
    ///     Represents the current selection state of path-based tools.
    /// </summary>
    public enum SelectionState {
        /// <summary>No nodes selected.</summary>
        NoSelection = 0,

        /// <summary>Start node selected, awaiting end node.</summary>
        StartNodeSelected = 1,

        /// <summary>Both start and end nodes selected, path is complete.</summary>
        EndNodeSelected = 2
    }

    /// <summary>
    ///     Abstract base class for tools that use path-based node selection.
    ///     Provides a complete state machine for selecting contiguous paths of network nodes.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Selection State Machine:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <term>NoSelection</term>
    ///             <description>All network nodes are eligible. Hover highlights single nodes. Apply transitions to StartNodeSelected.</description>
    ///         </item>
    ///         <item>
    ///             <term>StartNodeSelected</term>
    ///             <description>First node selected. Eligible nodes are those reachable without crossing intersections. Hover shows path preview. Apply transitions to EndNodeSelected.</description>
    ///         </item>
    ///         <item>
    ///             <term>EndNodeSelected</term>
    ///             <description>Path complete. Can extend by selecting additional nodes. Cancel pops the last node.</description>
    ///         </item>
    ///     </list>
    /// </remarks>
    public abstract partial class NT_PathSelectionToolSystem : NT_BaseToolSystem, INodeSelectionProvider {
        #region Template Methods

        /// <summary>
        ///     Called when a complete path is ready (2+ nodes selected).
        ///     Use this to initialize tool-specific state, create handles, etc.
        /// </summary>
        protected abstract void OnPathReady();

        /// <summary>
        ///     Called when the selection is cleared (back to NoSelection state).
        ///     Use this to clean up tool-specific state, destroy handles, etc.
        /// </summary>
        protected abstract void OnSelectionCleared();

        /// <summary>
        ///     Called when the path is extended with a new endpoint.
        ///     Override to update tool-specific state when the path grows.
        /// </summary>
        /// <param name="newEndNode">The newly added endpoint.</param>
        protected virtual void OnPathExtended(Entity newEndNode) { }

        /// <summary>
        ///     Called when the path is trimmed by removing the last node.
        ///     Override to update tool-specific state when the path shrinks.
        /// </summary>
        /// <param name="newEndNode">The new endpoint after trimming.</param>
        protected virtual void OnPathTrimmed(Entity newEndNode) { }

        /// <summary>
        ///     Called when hovering over an eligible node with a valid path preview.
        ///     Override to react to hover state changes.
        /// </summary>
        /// <param name="hoveredNode">The node being hovered.</param>
        protected virtual void OnPathPreviewUpdated(Entity hoveredNode) { }

        #endregion
    }
}
