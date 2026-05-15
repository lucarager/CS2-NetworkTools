namespace NetworkTools.Systems.Rendering {
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    ///     Render colors for overlay rendering. Passed to jobs as a value type.
    /// </summary>
    public struct Colors {
        // Node colors
        // -- Eligible Nodes
        public static Color NODE_ELIGIBLE_REST   = new(1f, 1f, 1f, 1f);
        public static Color NODE_ELIGIBLE_HOVER  = new(1f, 1f, 1f, 1f);
        public static Color NODE_ELIGIBLE_ACTIVE = new(1f, 1f, 1f, 1f);
        // -- Selected Nodes
        public static Color NODE_SELECTED_REST = new(1f, 1f, 0f, 1f);
        public static Color NODE_SELECTED_HOVER = new(1f, 1f, 0f, 1f);
        public static Color NODE_SELECTED_ACTIVE = new(1f, 1f, 0f, 1f);
        // -- Removed Nodes
        public static Color NODE_REMOVE_REST     = new(1f, 0f, 0f, 1f);
        public static Color NODE_REMOVE_HOVER = new(1f, 0f, 0f, 1f);
        public static Color NODE_REMOVE_ACTIVE = new(1f, 0f, 0f, 1f);
        // -- Added Nodes
        public static Color NODE_ADD_REST             = new(0f, 1f, 0f, 1f);
        public static Color NODE_ADD_HOVER = new(0f, 1f, 0f, 1f);
        public static Color NODE_ADD_ACTIVE = new(0f, 1f, 0f, 1f);
    }
}