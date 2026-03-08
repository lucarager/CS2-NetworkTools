namespace NetworkTools.Systems.Rendering {
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    ///     Render colors for overlay rendering. Passed to jobs as a value type.
    /// </summary>
    public struct RenderColors {
        // Node colors
        public float4 NodeSelectedFirstFill;
        public float4 NodeSelectedFirstBorder;
        public float4 NodeHighlightedFill;
        public float4 NodeHighlightedBorder;
        public float4 NodeEligibleFill;
        public float4 NodeEligibleBorder;

        // Edge colors
        public float4 EdgeSelected;
        public float4 EdgeHighlighted;

        // Handle colors - state-based
        public float4 HandleSelected;
        public float4 HandleHighlighted;
        public float4 HandleOrigin;

        // Handle colors - purpose-based
        public float4 HandleSlopeControl;
        public float4 HandleShapeControl;
        public float4 HandleDefault;

        // Temp entity colors
        public float4 TempEdge;
        public float4 TempNode;

        /// <summary>
        ///     Standard color values.
        /// </summary>
        public static readonly RenderColors Default = new RenderColors {
            // Node colors
            NodeSelectedFirstFill   = (Vector4)new Color(1f, 1f, 1f, 0.5f),
            NodeSelectedFirstBorder = (Vector4)new Color(1f, 1f, 1f, 1f),
            NodeHighlightedFill     = (Vector4)new Color(1f, 1f, 1f, 0.5f),
            NodeHighlightedBorder   = (Vector4)new Color(1f, 1f, 1f, 1f),
            NodeEligibleFill        = (Vector4)new Color(1f, 1f, 1f, 0.2f),
            NodeEligibleBorder      = (Vector4)new Color(1f, 1f, 1f, 0.6f),

            // Edge colors
            EdgeSelected    = (Vector4)new Color(0.58f, 0.27f, 1f, 1f),
            EdgeHighlighted = (Vector4)new Color(0.58f, 0.27f, 1f, 1f),

            // Handle colors - state-based
            HandleSelected    = (Vector4)new Color(0.5f, 0.2f, 0.8f, 1f),
            HandleHighlighted = (Vector4)new Color(0.6f, 0.37f, 0.9f, 1f),
            HandleOrigin      = (Vector4)new Color(1f, 1f, 1f, 1f),

            // Handle colors - purpose-based
            HandleSlopeControl = (Vector4)new Color(0.5f, 0.7f, 1f, 1f),
            HandleShapeControl = (Vector4)new Color(0.5f, 0.7f, 1f, 1f),
            HandleDefault      = (Vector4)new Color(0.4f, 0.6f, 1f, 1f),

            // Temp entity colors
            TempEdge = (Vector4)new Color(1f, 1f, 1f, 1f),
            TempNode = (Vector4)new Color(1f, 1f, 1f, 1f),
        };
    }
}