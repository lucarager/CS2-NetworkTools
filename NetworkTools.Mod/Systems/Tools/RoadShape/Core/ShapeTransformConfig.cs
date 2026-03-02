namespace NetworkTools.Systems.Tools.RoadShape {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Colossal.UI.Binding;

    using Unity.Mathematics;

    /// <summary>
    ///     Defines the type of transformation to apply
    /// </summary>
    public enum ShapeTransformTemplate {
        /// <summary>
        ///     Keep existing positions (no-op).
        /// </summary>
        Preserve = 0,

        /// <summary>
        ///     Linear slope interpolation - constant slope throughout the path.
        /// </summary>
        SlopeLinear = 1,

        /// <summary>
        ///     Ease-in-out slope - smooth transitions at start and end with steeper middle section.
        /// </summary>
        SlopeEaseInOut = 2,

        /// <summary>
        ///     Arch slope - creates an arch (hill) or dip (valley) along the path.
        /// </summary>
        SlopeArch = 3,

        /// <summary>
        ///     Straight curve - Align all nodes along a straight line between start/end.
        /// </summary>
        CurveStraighten = 4,

        /// <summary>
        ///     Smooth curve - Fit nodes to a smooth Bézier curve.
        /// </summary>
        CurveSmooth = 5,
    }

    public struct ShapeTransformConfig : IJsonWritable, IJsonReadable {
        /// <summary>
        ///     The template type to use for shape calculation.
        /// </summary>
        public ShapeTransformTemplate Template;

        /// <summary>
        ///     Whether to render slope tooltips.
        /// </summary>
        public bool RenderSlopeTooltips;

        #region Preserve

        /// <summary>
        ///     Creates a preserve configuration (keeps existing Y positions).
        /// </summary>
        public static ShapeTransformConfig Preserve() {
            return new ShapeTransformConfig {
                Template = ShapeTransformTemplate.Preserve
            };
        }

        #endregion

        #region SlopeLinear

        /// <summary>
        ///     Creates a linear slope configuration (default).
        /// </summary>
        public static ShapeTransformConfig SlopeLinear() {
            return new ShapeTransformConfig {
                Template = ShapeTransformTemplate.SlopeLinear,
                RenderSlopeTooltips = true,
            };
        }

        #endregion

        #region SlopeEaseInOut

        /// <summary>
        ///     Length of ease-in transition at start (0 to 0.5).
        ///     Defines how much of the path has gradual slope increase.
        /// </summary>
        public float EaseInLength;

        /// <summary>
        ///     Length of ease-out transition at end (0 to 0.5).
        ///     Defines how much of the path has gradual slope decrease.
        /// </summary>
        public float EaseOutLength;

        public static float EaseInMax  = 0.4f;
        public static float EaseOutMax = 0.4f;

        /// <summary>
        ///     Creates an ease-in-out configuration with specified transition lengths.
        /// </summary>
        /// <param name="easeInLength">Ease-in control point strength</param>
        /// <param name="easeOutLength">Ease-out control point strength</param>
        public static ShapeTransformConfig SlopeEaseInOut(float easeInLength = 0.1f, float easeOutLength = 0.1f) {
            return new ShapeTransformConfig {
                Template            = ShapeTransformTemplate.SlopeEaseInOut,
                RenderSlopeTooltips = true,
                EaseInLength  = math.clamp(easeInLength, 0f, EaseInMax),
                EaseOutLength = math.clamp(easeOutLength, 0f, EaseOutMax),
            };
        }

        #endregion

        #region SlopeArch

        /// <summary>
        ///     Height of the arch (-1 to 1).
        ///     Positive creates a hill, negative creates a valley.
        /// </summary>
        public float ArchHeight;

        /// <summary>
        ///     Position of the arch peak/valley along path (0 to 1).
        ///     0.5 places it in the middle.
        /// </summary>
        public float ArchPosition;

        /// <summary>
        ///     Creates an arch configuration with specified arch properties.
        /// </summary>
        /// <param name="height">Arch height (-1 to 1, negative = valley, positive = hill)</param>
        /// <param name="position">Arch position (0 to 1, 0.5 = middle)</param>
        public static ShapeTransformConfig SlopeArch(float height = 0.5f, float position = 0.5f) {
            return new ShapeTransformConfig {
                Template     = ShapeTransformTemplate.SlopeArch,
                ArchHeight   = math.clamp(height,   -1f,  1f),
                ArchPosition = math.clamp(position, 0.1f, 0.9f)
            };
        }

        #endregion

        #region CurveStraighten

        /// <summary>
        /// Creates a straighten configuration.
        /// </summary>
        public static ShapeTransformConfig CurveStraighten() => new ShapeTransformConfig {
            Template = ShapeTransformTemplate.CurveStraighten,
        };

        #endregion

        #region CurveSmooth

        /// <summary>
        /// Smoothing factor (0-1), how much to smooth.
        /// Used with Smooth template.
        /// </summary>
        public float SmoothingFactor;

        /// <summary>
        ///     Creates a smooth configuration with the specified smoothing factor.
        /// </summary>
        /// <param name="factor">Smoothing factor (0 to 1).</param>
        public static ShapeTransformConfig CurveSmooth(float factor = 0.5f) => new ShapeTransformConfig {
            Template        = ShapeTransformTemplate.CurveSmooth,
            SmoothingFactor = math.clamp(factor, 0f, 1f),
        };

        #endregion


        /// <inheritdoc/>
        public void Write(IJsonWriter writer) {
            writer.TypeBegin(GetType().FullName);

            writer.PropertyName("template");
            writer.Write((int)Template);
            
            writer.PropertyName("easeInLength");
            writer.Write(EaseInLength);

            writer.PropertyName("easeOutLength");
            writer.Write(EaseOutLength);

            writer.PropertyName("smoothingFactor");
            writer.Write(SmoothingFactor);

            writer.PropertyName("archHeight");
            writer.Write(ArchHeight);

            writer.PropertyName("archPosition");
            writer.Write(ArchPosition);

            writer.TypeEnd();
        }

        /// <inheritdoc/>
        public void Read(IJsonReader reader) {
            reader.ReadMapBegin();

            if (reader.ReadProperty("template"))
            {
                reader.Read(out int template);
                Template = (ShapeTransformTemplate)template;
            }

            if (reader.ReadProperty("easeInLength"))
            {
                reader.Read(out float easeInLength);
                EaseInLength = easeInLength;
            }

            if (reader.ReadProperty("easeOutLength"))
            {
                reader.Read(out float easeOutLength);
                EaseOutLength = easeOutLength;
            }

            if (reader.ReadProperty("smoothingFactor"))
            {
                reader.Read(out float smoothingFactor);
                SmoothingFactor = smoothingFactor;
            }

            if (reader.ReadProperty("archHeight"))
            {
                reader.Read(out float archHeight);
                ArchHeight = archHeight;
            }

            if (reader.ReadProperty("archPosition"))
            {
                reader.Read(out float archPosition);
                ArchPosition = archPosition;
            }

            reader.ReadMapEnd();
        }
    }
}
