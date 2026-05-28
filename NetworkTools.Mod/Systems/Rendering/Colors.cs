namespace NetworkTools.Systems.Rendering {
    using UnityEngine;

    public readonly struct ColorPair {
        public readonly Color Fill;
        public readonly Color Border;

        public ColorPair(Color fill, Color border) {
            Fill   = fill;
            Border = border;
        }
    }

    public readonly struct ColorSet {
        public readonly ColorPair Rest;
        public readonly ColorPair Hover;
        public readonly ColorPair Active;

        public ColorSet(ColorPair rest, ColorPair hover, ColorPair active) {
            Rest   = rest;
            Hover  = hover;
            Active = active;
        }

        public ColorPair Get(bool isHighlighted, bool isSelected) {
            if (isSelected) {
                return Active;
            }

            if (isHighlighted) {
                return Hover;
            }

            return Rest;
        }

        public static ColorSet FromPair(Color fill, Color border) {
            var pair = new ColorPair(fill, border);
            return new ColorSet(pair, pair, pair);
        }
    }

    public static class NT_Colors {
        // -- Node colors
        // ---- Visible Nodes (Visible but cannot be interacted with)
        public static readonly ColorSet NodeVisible = ColorSet.FromPair(new Color(0.788f, 0.776f, 0.824f, 0.8f),
                                                                        new Color(0f,     0f,     0f,     0f));

        // ---- Eligible Nodes (Can be interacted with)
        public static readonly ColorSet NodeEligible =
            new(new ColorPair(new Color(0.745f, 0.733f, 0.784f, 0.8f), new Color(0f, 0f, 0f, 0f)),
                new ColorPair(new Color(0.745f, 0.733f, 0.784f, 0.8f), new Color(1f, 1f, 1f, 1f)),
                new ColorPair(new Color(1f,     1f,     1f,     0.8f), new Color(1f, 1f, 1f, 1f)));

        // ---- Selected Nodes
        public static readonly ColorSet NodeSelected =
            new(new ColorPair(new Color(0.702f, 0.365f, 1f, 0.8f), new Color(0f,     0f,     0f, 0f)),
                new ColorPair(new Color(0.702f, 0.365f, 1f, 0.8f), new Color(0.788f, 0.545f, 1f, 1f)),
                new ColorPair(new Color(0.769f, 0.510f, 1f, 0.8f), new Color(0.788f, 0.545f, 1f, 1f)));

        // ---- Added Nodes
        public static readonly ColorSet NodeAdded = ColorSet.FromPair(new Color(0.310f, 0.894f, 0.573f, 0.8f),
                                                                      new Color(0f,     0f,     0f,     0f));

        // ---- Removed Nodes
        public static readonly ColorSet NodeRemoved = ColorSet.FromPair(new Color(0.894f, 0.310f, 0.310f, 0.8f),
                                                                        new Color(0f,     0f,     0f,     0f));

        // -- Edge Colors
        // ---- Visible Edges (Visible but cannot be interacted with)
        public static readonly ColorSet EdgeVisible = ColorSet.FromPair(new Color(0.788f, 0.776f, 0.824f, 0.8f),
                                                                        new Color(0f,     0f,     0f,     0f));

        // ---- Eligible Edges (Can be interacted with)
        public static readonly ColorSet EdgeEligible =
            new(new ColorPair(new Color(0.745f, 0.733f, 0.784f, 0.8f), new Color(0f, 0f, 0f, 0f)),
                new ColorPair(new Color(0.745f, 0.733f, 0.784f, 0.8f), new Color(1f, 1f, 1f, 1f)),
                new ColorPair(new Color(1f,     1f,     1f,     0.8f), new Color(1f, 1f, 1f, 1f)));

        // -- Handle Colors
        // ---- Line Handles
        public static readonly Color HandleLineFill = new(0.310f, 0.794f, 0.873f, 0.8f);

        // ---- Circle Handles
        public static readonly ColorSet HandleCircle =
            new(new ColorPair(new Color(0.788f, 0.545f, 0.873f, 0f), new Color(0.788f, 0.545f, 0.873f, 0.5f)),
                new ColorPair(new Color(0.788f, 0.545f, 0.873f, 0f), new Color(0.788f, 0.545f, 0.873f, 0.8f)),
                new ColorPair(new Color(0.788f, 0.545f, 0.873f, 0f), new Color(0.788f, 0.545f, 0.873f, 1f)));

        // ---- Parameter Range Handles
        public static readonly ColorSet HandleParamRangeOrigin =
            new(new ColorPair(new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0)),
                new ColorPair(new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0)),
                new ColorPair(new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0)));

        public static readonly ColorSet HandleParamRangeLine =
            new(new ColorPair(new Color(0.310f, 0.794f, 0.873f, 0.5f), new Color(0.310f, 0.794f, 0.873f, 0.5f)),
                new ColorPair(new Color(0.310f, 0.794f, 0.873f, 0.8f), new Color(0.310f, 0.794f, 0.873f, 0.8f)),
                new ColorPair(new Color(0.310f, 0.794f, 0.873f, 1f), new Color(0.310f, 0.794f, 0.873f, 1f)));

        public static readonly ColorSet HandleParamRangeCircle =
            new(new ColorPair(new Color(0.310f, 0.794f, 0.873f, 0f), new Color(0.310f, 0.794f, 0.873f, 0.5f)),
                new ColorPair(new Color(0.310f, 0.794f, 0.873f, 0f), new Color(0.310f, 0.794f, 0.873f, 0.8f)),
                new ColorPair(new Color(0.310f, 0.794f, 0.873f, 0f), new Color(0.310f, 0.794f, 0.873f, 1f)));

        // ---- Point Range Handles
        public static readonly ColorSet HandlePoint =
            new(new ColorPair(new Color(0.310f, 0.794f, 0.873f, 0f), new Color(0.310f, 0.794f, 0.873f, 0.5f)),
                new ColorPair(new Color(0.310f, 0.794f, 0.873f, 0f), new Color(0.310f, 0.794f, 0.873f, 0.8f)),
                new ColorPair(new Color(0.310f, 0.794f, 0.873f, 0f), new Color(0.310f, 0.794f, 0.873f, 1f)));
    }
}