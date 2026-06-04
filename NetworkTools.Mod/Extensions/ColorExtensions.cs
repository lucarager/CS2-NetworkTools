namespace NetworkTools.Extensions {
    using UnityEngine;

    public static class ColorExtensions {
        public static Color WithAlpha(this Color color, float alpha) {
            return new Color(color.r, color.g, color.b, alpha);
        }

        public static Color Lighten(this Color color, float amount) {
            return new Color(color.r + (1f - color.r) * amount,
                             color.g + (1f - color.g) * amount,
                             color.b + (1f - color.b) * amount,
                             color.a);
        }

        public static Color Darken(this Color color, float amount) {
            return new Color(color.r * (1f - amount),
                             color.g * (1f - amount),
                             color.b * (1f - amount),
                             color.a);
        }
    }
}
