namespace NetworkTools.Systems.Rendering {
    using Unity.Mathematics;
    using UnityEngine;

    public static class NT_Colors {
        // -- Node colors
        // ---- Visible Nodes (Visible but cannot be interacted with)
        public static readonly Color NODE_VISIBLE_FILL   = new(0.788f, 0.776f, 0.824f, 0.8f);
        public static readonly Color NODE_VISIBLE_BORDER = new(0f, 0f, 0f, 0f);

        // ---- Eligible Nodes (Can be interacted with)
        public static readonly Color NODE_ELIGIBLE_REST_FILL     = new(0.745f, 0.733f, 0.784f, 0.8f);
        public static readonly Color NODE_ELIGIBLE_REST_BORDER   = new(0f, 0f, 0f, 0f);
        public static readonly Color NODE_ELIGIBLE_HOVER_FILL    = new(0.745f, 0.733f, 0.784f, 0.8f);
        public static readonly Color NODE_ELIGIBLE_HOVER_BORDER  = new(1f, 1f, 1f, 1f);
        public static readonly Color NODE_ELIGIBLE_ACTIVE_FILL   = new(1f, 1f, 1f, 0.8f);
        public static readonly Color NODE_ELIGIBLE_ACTIVE_BORDER = new(1f, 1f, 1f, 1f);

        // ---- Selected Nodes
        public static readonly Color NODE_SELECTED_REST_FILL     = new(0.702f, 0.365f, 1f, 0.8f);
        public static readonly Color NODE_SELECTED_REST_BORDER   = new(0f, 0f, 0f, 0f);
        public static readonly Color NODE_SELECTED_HOVER_FILL    = new(0.702f, 0.365f, 1f, 0.8f);
        public static readonly Color NODE_SELECTED_HOVER_BORDER  = new(0.788f, 0.545f, 1f, 1f);
        public static readonly Color NODE_SELECTED_ACTIVE_FILL   = new(0.769f, 0.510f, 1f, 0.8f);
        public static readonly Color NODE_SELECTED_ACTIVE_BORDER = new(0.788f, 0.545f, 1f, 1f);

        // ---- Added Nodes
        public static readonly Color NODE_ADDED_FILL   = new(0.310f, 0.894f, 0.573f, 0.8f);
        public static readonly Color NODE_ADDED_BORDER = new(0f, 0f, 0f, 0f);

        // ---- Removed Nodes
        public static readonly Color NODE_REMOVED_FILL   = new(0.894f, 0.310f, 0.310f, 0.8f);
        public static readonly Color NODE_REMOVED_BORDER = new(0f, 0f, 0f, 0f);

        //  -- Edge Colors
        // ---- Visible Edges (Visible but cannot be interacted with)
        public static readonly Color EDGE_VISIBLE_FILL = new(0.788f, 0.776f, 0.824f, 0.8f);
        public static readonly Color EDGE_VISIBLE_BORDER = new(0f, 0f, 0f, 0f);
        
        // ---- Eligible Edges (Can be interacted with)
        public static readonly Color EDGE_ELIGIBLE_REST_FILL = new(0.745f, 0.733f, 0.784f, 0.8f);
        public static readonly Color EDGE_ELIGIBLE_REST_BORDER = new(0f, 0f, 0f, 0f);
        public static readonly Color EDGE_ELIGIBLE_HOVER_FILL = new(0.745f, 0.733f, 0.784f, 0.8f);
        public static readonly Color EDGE_ELIGIBLE_HOVER_BORDER = new(1f, 1f, 1f, 1f);
        public static readonly Color EDGE_ELIGIBLE_ACTIVE_FILL = new(1f, 1f, 1f, 0.8f);
        public static readonly Color EDGE_ELIGIBLE_ACTIVE_BORDER = new(1f, 1f, 1f, 1f);
    }
}