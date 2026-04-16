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

        // Handle colors 
        public float4 HandleFillRest;
        public float4 HandleFillHover;
        public float4 HandleFillSelected;
        public float4 HandleOutlineRest;
        public float4 HandleOutlineHover;
        public float4 HandleOutlineSelected;
        public float4 HandleLineRest;
        public float4 HandleLineHover;
        public float4 HandleLineSelected;
        public float4 HandleSecondaryLineRest;
        public float4 HandleSecondaryLineHover;
        public float4 HandleSecondaryLineSelected;

        // Temp entity colors
        public float4 TempEdge;
        public float4 TempNode;

        // AddNode Colors
        public float4 AddNodeEdgeEligible;
        public float4 AddNodeEdgeTemp;
        public float4 AddNodeNode;

        /// <summary>
        ///     Standard color values.
        /// </summary>
        public static readonly RenderColors Default = new() {
            // Node colors
            NodeSelectedFirstFill   = (Vector4)new Color(1f, 1f, 1f, 0.5f),
            NodeSelectedFirstBorder = (Vector4)new Color(1f, 1f, 1f, 1f),
            NodeHighlightedFill     = (Vector4)new Color(0.58f, 0.27f, 1f, 0.5f),
            NodeHighlightedBorder   = (Vector4)new Color(0.58f, 0.27f, 1f, 1f),
            NodeEligibleFill        = (Vector4)new Color(1f, 1f, 1f, 0.2f),
            NodeEligibleBorder      = (Vector4)new Color(1f, 1f, 1f, 0.6f),

            // Edge colors
            EdgeSelected    = (Vector4)new Color(0.58f, 0.27f, 1f, 1f),
            EdgeHighlighted = (Vector4)new Color(0.58f, 0.27f, 1f, 1f),

            // Handle colors
            HandleFillRest              = (Vector4)new Color(1f,    1f,    1f,    1f),
            HandleFillHover             = (Vector4)new Color(1f,    1f,    1f,    1f),
            HandleFillSelected          = (Vector4)new Color(1f,    1f,    1f,    1f),
            HandleOutlineRest           = (Vector4)new Color(0.68f, 0.35f, 1f,    1f),
            HandleOutlineHover          = (Vector4)new Color(0.85f, 0.55f, 1f,    1f),
            HandleOutlineSelected       = (Vector4)new Color(0.85f, 0.55f, 1f,    1f),
            HandleLineRest              = (Vector4)new Color(0.68f, 0.35f, 1f,    1f),
            HandleLineHover             = (Vector4)new Color(0.85f, 0.55f, 1f,    1f),
            HandleLineSelected          = (Vector4)new Color(0.85f, 0.55f, 1f,    1f),
            HandleSecondaryLineRest     = (Vector4)new Color(0.85f, 0.85f, 0.85f, 0.5f),
            HandleSecondaryLineHover    = (Vector4)new Color(0.85f, 0.85f, 0.85f, 1f),
            HandleSecondaryLineSelected = (Vector4)new Color(0.85f, 0.85f, 0.85f, 1f),

            // Temp entity colors
            TempEdge = (Vector4)new Color(1f, 1f, 1f, 1f),
            TempNode = (Vector4)new Color(1f, 1f, 1f, 1f),

            // AddNode Colors
            AddNodeEdgeEligible = (Vector4)new Color(1f, 1f, 1f, 0.3f),
            AddNodeEdgeTemp = (Vector4)new Color(1f, 1f, 1f, 1f),
            AddNodeNode = (Vector4)new Color(1f, 1f, 1f, 1f),
        };
    }
}