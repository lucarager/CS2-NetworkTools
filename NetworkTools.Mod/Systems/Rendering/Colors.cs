namespace NetworkTools.Systems.Rendering {
    using NetworkTools.Extensions;
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
        // -- Colors
        public static readonly Color Teal        = new Color(0.5f,  0.9f,  0.7f,  1f);
        public static readonly Color Blue        = new Color(0.3f,  0.8f,  0.9f,  1f);
        public static readonly Color Purple      = new Color(0.7f,  0.35f, 1f,    1f);
        public static readonly Color PurpleLight = new Color(0.8f,  0.55f, 0.85f, 1f);
        public static readonly Color Gray        = new Color(0.75f, 0.75f, 0.8f,  1f);
        public static readonly Color White       = new Color(1f,    1f,    1f,    1f);


        // -- Node colors
        // ---- Visible Nodes (Visible but cannot be interacted with)
        public static readonly ColorSet NodeVisible = ColorSet.FromPair(new Color(0.788f, 0.776f, 0.824f, 0.8f),
                                                                        new Color(0f,     0f,     0f,     0f));

        // ---- Eligible Nodes (Can be interacted with)
        public static readonly ColorSet NodeEligible =
            new(new ColorPair(Gray.WithAlpha(0.8f), Gray.WithAlpha(0f)),
                new ColorPair(Gray.WithAlpha(0.8f), White),
                new ColorPair(White.WithAlpha(0.8f), White));

        // ---- Selected Nodes
        public static readonly ColorSet NodeSelected =
            new(new ColorPair(Purple.WithAlpha(0.8f), PurpleLight.WithAlpha(0f)),
                new ColorPair(Purple.WithAlpha(0.8f), PurpleLight.WithAlpha(1f)),
                new ColorPair(Purple.Lighten(0.1f).WithAlpha(0.8f), PurpleLight.WithAlpha(1f)));

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
                new ColorPair(new Color(0.745f, 0.733f, 0.784f, 0.8f), White),
                new ColorPair(new Color(1f,     1f,     1f,     0.8f), White));

        // -- Handle Colors
        // ---- Line Handles
        public static readonly Color HandleLineFill = new(0.310f, 0.794f, 0.873f, 0.8f);

        // ---- Circle Handles
        public static readonly ColorSet HandleCircle =
            new(new ColorPair(PurpleLight.WithAlpha(0f), PurpleLight.WithAlpha(0.5f)),
                new ColorPair(PurpleLight.WithAlpha(0f), PurpleLight.WithAlpha(0.8f)),
                new ColorPair(PurpleLight.WithAlpha(0f), PurpleLight.WithAlpha(1f)));


        // ---- Rotation Handles
        public static readonly ColorSet HandleRotation =
            new(new ColorPair(Teal.WithAlpha(0f), Teal.WithAlpha(0.5f)),
                new ColorPair(Teal.WithAlpha(0f), Teal.WithAlpha(0.8f)),
                new ColorPair(Teal.WithAlpha(0f), Teal.WithAlpha(1f)));

        // ---- Axis Handles
        public static readonly ColorSet HandleAxisOrigin =
            new(new ColorPair(White, White.WithAlpha(0f)),
                new ColorPair(White, White.WithAlpha(0f)),
                new ColorPair(White, White.WithAlpha(0f)));

        public static readonly ColorSet HandleAxisLine =
            new(new ColorPair(Blue.WithAlpha(0.5f), Blue.WithAlpha(0.5f)),
                new ColorPair(Blue.WithAlpha(0.8f), Blue.WithAlpha(0.8f)),
                new ColorPair(Blue.WithAlpha(1f), Blue.WithAlpha(1f)));

        public static readonly ColorSet HandleAxisCircle =
            new(new ColorPair(Blue.WithAlpha(0f), Blue.WithAlpha(0.5f)),
                new ColorPair(Blue.WithAlpha(0f), Blue.WithAlpha(0.8f)),
                new ColorPair(Blue.WithAlpha(0f), Blue.WithAlpha(1f)));

        // ---- Point Handles
        public static readonly ColorSet HandlePoint =
            new(new ColorPair(Blue.WithAlpha(0f), Blue.WithAlpha(0.5f)),
                new ColorPair(Blue.WithAlpha(0f), Blue.WithAlpha(0.8f)),
                new ColorPair(Blue.WithAlpha(0f), Blue.WithAlpha(1f)));
    }
}