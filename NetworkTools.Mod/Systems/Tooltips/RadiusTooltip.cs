namespace NetworkTools.Systems.Tooltips {
    using Colossal.UI.Binding;

    using Game.UI.Tooltip;

    /// <summary>
    ///     A single radius readout, drawn next to the handle that sets it.
    ///     <para>
    ///         Kept as its own widget type rather than reusing a vanilla one so the label can say
    ///         <b>R</b> and still go through the game's unit formatting (the player's metric/imperial
    ///         setting and the localized decimal separator both apply).
    ///     </para>
    /// </summary>
    public class RadiusTooltip : LabelIconTooltip {
        private float          m_Radius;
        private IWriter<float> m_RadiusWriter;

        public float Radius {
            get => m_Radius;
            set {
                if (value.Equals(m_Radius)) return;

                m_Radius = value;
                SetPropertiesChanged();
            }
        }

        protected IWriter<float> radiusWriter {
            get {
                if (m_RadiusWriter == null) {
                    m_RadiusWriter = ValueWriters.Create<float>();
                }

                return m_RadiusWriter;
            }
            set => m_RadiusWriter = value;
        }

        public override string propertiesTypeName => "NT.RadiusTooltip";

        protected override void WriteProperties(IJsonWriter writer) {
            base.WriteProperties(writer);
            writer.PropertyName("Radius");
            radiusWriter.Write(writer, Radius);
        }
    }
}
