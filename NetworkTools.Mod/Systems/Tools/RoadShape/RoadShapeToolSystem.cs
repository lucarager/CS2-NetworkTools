namespace NetworkTools.Systems.Tools.RoadShape {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    ///     Represents the currentEntity selection state of the tool.
    /// </summary>
    public enum SelectionState {
        NoSelection = 0,
        StartNodeSelected = 1,
        EndNodeSelected = 2
    }

    public partial class NT_RoadShapeToolSystem : NT_BaseToolSystem {
        public override string toolID => "RoadShapeTool";

        /// <summary>
        ///     Currently selected path of edges
        /// </summary>
        private NativeList<Entity> m_CurrentPathEdges;

        /// <summary>
        ///     Currently selected path of nodes
        /// </summary>
        private NativeList<Entity> m_CurrentPathNodes;

        /// <summary>
        ///     List of currently eligible node entities for selection
        /// </summary>
        private NativeList<Entity> m_EligibleNodes;

        /// <summary>
        ///     Caches the last hit position
        /// </summary>
        private float3 m_LastHitPosition;

        /// <summary>
        ///     Next path of edges (updated on hover)
        /// </summary>
        private NativeList<Entity> m_NextPathEdges;

        /// <summary>
        ///     Next path of nodes (updated on hover)
        /// </summary>
        private NativeList<Entity> m_NextPathNodes;

        /// <summary>
        ///     List of currently selected node entities, creating a contiguous path
        /// </summary>
        private NativeList<Entity> m_SelectedNodes;

        /// <summary>
        ///     Current selection state (Happens during Configuring phase of OperationState)
        ///     ## m_OperationState machine:
        ///     ### NoSelection
        ///     - All network nodes in the game have NT_Eligible component
        ///     - Actions:
        ///     - [Hover] over NT_Eligible Node: Clear NT_Highlighted. Adds NT_Highlighted to node.
        ///     - [Hover] over nothing: Removes all NT_Highlighted.
        ///     - [Apply]: Transition to `StartNodeSelected` with node.
        ///     - [Cancel]: Exit Tool
        ///     ### StartNodeSelected
        ///     - When entering state with node, adds this node to the start of the "Nodes" list. This node is now the start node
        ///     - First node has: NT_Selected, NT_SelectedFirst
        ///     - Eligible nodes are nodes reachable via an uninterrupted edge (no intersections) from the start node.
        ///     - Any eligible nodes have: NT_Eligible
        ///     - Actions:
        ///     - [Hover] over NT_Eligible Node: Clear NT_Highlighted. Adds NT_Highlighted to node. Add NT_Highlighted to Edges and
        ///     Nodes between start and hovered node.
        ///     - [Hover] over nothing: Removes all NT_Highlighted.
        ///     - [Apply]: Transition to `EndNodeSelected` with node.
        ///     - [Cancel]: Transition back to `NoSelection`
        ///     ### EndNodeSelected
        ///     - When entering state with node, adds this node to the "Nodes" list. The new node is now the end node.
        ///     - First node has: NT_Selected, NT_SelectedFirst
        ///     - Last node has: NT_Selected, NT_SelectedLast
        ///     - Edges and Nodes in path between the two have: NT_Selected
        ///     - Eligible nodes are nodes reachable via an uninterrupted edge (no intersections) from the end node. This allows
        ///     "extending" the selected edge beyond intersections.
        ///     - Any eligible nodes have: NT_Eligible
        ///     - Actions:
        ///     - [Hover] over NT_Eligible Node: Clear NT_Highlighted. Adds NT_Highlighted to node. Add NT_Highlighted to Edges and
        ///     Nodes between currentEntity end node and hovered node.
        ///     - [Hover] over nothing: Removes all NT_Highlighted.
        ///     - [Apply]: Transition to `EndNodeSelected` with new end node.
        ///     - [Cancel]: Pop last node from cache. If it's the last "end node", transition back to `StartNodeSelected`
        /// </summary>
        public SelectionState CurrentSelectionState =>
            m_SelectedNodes.Length switch {
                0 => SelectionState.NoSelection,
                1 => SelectionState.StartNodeSelected,
                _ => SelectionState.EndNodeSelected
            };

        /// <summary>
        ///     Tracks whether an update/re-render is needed on the next frame.
        ///     This is set to true when something changes that requires regenerating preview entities.
        ///     Gets reset to false after being processed.
        /// </summary>
        private bool m_UpdateNeeded;


        /// <summary>
        ///     Current config
        /// </summary>
        internal ShapeTransformConfig ShapeTransformConfig;

        /// <summary>
        ///     Gets the array of currently selected node entities.
        /// </summary>
        /// <returns>Array of selected Entity objects.</returns>
        public Entity[] GetSelectedNodes() {
            return m_SelectedNodes.ToArray(Allocator.Temp).ToArray();
        }
    }
}
