namespace NetworkTools.Systems.Rendering {
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    ///     Enum defining all available render color keys for overlay rendering.
    /// </summary>
    public enum RenderColorKey : byte {
        // Node colors
        NodeSelectedFirstFill,
        NodeSelectedFirstBorder,
        NodeHighlightedFill,
        NodeHighlightedBorder,
        NodeEligibleFill,
        NodeEligibleBorder,

        // Edge colors
        EdgeSelected,
        EdgeHighlighted,

        // Handle colors - state-based
        HandleSelected,
        HandleHighlighted,
        HandleOrigin,

        // Handle colors - purpose-based
        HandleSlopeControl,
        HandleShapeControl,
        HandleDefault,

        // Temp entity colors
        TempEdge,
        TempNode
    }

    /// <summary>
    ///     Struct containing all render colors for overlay rendering.
    ///     Passed to jobs to provide centralized color configuration.
    /// </summary>
    public readonly struct RenderColors {
        // Node colors
        public readonly float4 NodeSelectedFirstFill;
        public readonly float4 NodeSelectedFirstBorder;
        public readonly float4 NodeHighlightedFill;
        public readonly float4 NodeHighlightedBorder;
        public readonly float4 NodeEligibleFill;
        public readonly float4 NodeEligibleBorder;

        // Edge colors
        public readonly float4 EdgeSelected;
        public readonly float4 EdgeHighlighted;

        // Handle colors - state-based
        public readonly float4 HandleSelected;
        public readonly float4 HandleHighlighted;
        public readonly float4 HandleOrigin;

        // Handle colors - purpose-based
        public readonly float4 HandleSlopeControl;
        public readonly float4 HandleShapeControl;
        public readonly float4 HandleDefault;

        // Temp entity colors
        public readonly float4 TempEdge;
        public readonly float4 TempNode;

        /// <summary>
        ///     Gets a color by its key.
        /// </summary>
        public float4 this[RenderColorKey key] => key switch {
            RenderColorKey.NodeSelectedFirstFill   => NodeSelectedFirstFill,
            RenderColorKey.NodeSelectedFirstBorder => NodeSelectedFirstBorder,
            RenderColorKey.NodeHighlightedFill     => NodeHighlightedFill,
            RenderColorKey.NodeHighlightedBorder   => NodeHighlightedBorder,
            RenderColorKey.NodeEligibleFill        => NodeEligibleFill,
            RenderColorKey.NodeEligibleBorder      => NodeEligibleBorder,
            RenderColorKey.EdgeSelected            => EdgeSelected,
            RenderColorKey.EdgeHighlighted         => EdgeHighlighted,
            RenderColorKey.HandleSelected          => HandleSelected,
            RenderColorKey.HandleHighlighted       => HandleHighlighted,
            RenderColorKey.HandleOrigin            => HandleOrigin,
            RenderColorKey.HandleSlopeControl      => HandleSlopeControl,
            RenderColorKey.HandleShapeControl      => HandleShapeControl,
            RenderColorKey.HandleDefault           => HandleDefault,
            RenderColorKey.TempEdge                => TempEdge,
            RenderColorKey.TempNode                => TempNode,
            _                                      => HandleDefault
        };

        /// <summary>
        ///     Creates a default RenderColors instance with standard color values.
        /// </summary>
        public static RenderColors Default => new(
                                                  // Node colors
                                                  (Vector4)new Color(1f, 1f, 1f, 0.5f),
                                                  (Vector4)new Color(1f, 1f, 1f, 1f),
                                                  (Vector4)new Color(1f, 1f, 1f, 0.5f),
                                                  (Vector4)new Color(1f, 1f, 1f, 1f),
                                                  (Vector4)new Color(1f, 1f, 1f, 0.2f),
                                                  (Vector4)new Color(1f, 1f, 1f, 0.6f),

                                                  // Edge colors
                                                  (Vector4)new Color(0.58f, 0.27f, 1f, 1f),
                                                  (Vector4)new Color(0.58f, 0.27f, 1f, 1f),

                                                  // Handle colors - state-based
                                                  (Vector4)new Color(0.5f, 0.2f, .8f, 1f),
                                                  (Vector4)new Color(0.6f, 0.37f, .9f, 1f),
                                                  (Vector4)new Color(1f,   1f,   1f,   1f),

                                                  // Handle colors - purpose-based
                                                  (Vector4)new Color(0.5f, 0.7f, 1f,   1f),
                                                  (Vector4)new Color(0.5f, 0.7f, 1f, 1f),
                                                  (Vector4)new Color(0.4f, 0.6f, 1f,   1f),

                                                  // Temp entity colors
                                                  (Vector4)new Color(1f, 1f, 1f, 1f),
                                                  (Vector4)new Color(1f, 1f, 1f, 1f));

        private RenderColors(
            float4 nodeSelectedFirstFill,
            float4 nodeSelectedFirstBorder,
            float4 nodeHighlightedFill,
            float4 nodeHighlightedBorder,
            float4 nodeEligibleFill,
            float4 nodeEligibleBorder,
            float4 edgeSelected,
            float4 edgeHighlighted,
            float4 handleSelected,
            float4 handleHighlighted,
            float4 handleOrigin,
            float4 handleSlopeControl,
            float4 handleShapeControl,
            float4 handleDefault,
            float4 tempEdge,
            float4 tempNode) {
            NodeSelectedFirstFill   = nodeSelectedFirstFill;
            NodeSelectedFirstBorder = nodeSelectedFirstBorder;
            NodeHighlightedFill     = nodeHighlightedFill;
            NodeHighlightedBorder   = nodeHighlightedBorder;
            NodeEligibleFill        = nodeEligibleFill;
            NodeEligibleBorder      = nodeEligibleBorder;
            EdgeSelected            = edgeSelected;
            EdgeHighlighted         = edgeHighlighted;
            HandleSelected          = handleSelected;
            HandleHighlighted       = handleHighlighted;
            HandleOrigin            = handleOrigin;
            HandleSlopeControl      = handleSlopeControl;
            HandleShapeControl      = handleShapeControl;
            HandleDefault           = handleDefault;
            TempEdge                = tempEdge;
            TempNode                = tempNode;
        }
    }
}