namespace NetworkTools.Systems.Tooltips {
    using Colossal.UI.Binding;
    using Game.UI.Tooltip;

    public class SlopeTooltip : LabelIconTooltip {
        private float          m_CurrentSlope;
        private float          m_NewSlope;
        private IWriter<float> m_SlopeWriter;

        public float CurrentSlope {
            get => m_CurrentSlope;
            set {
                if (value.Equals(m_CurrentSlope)) {
                    return;
                }

                m_CurrentSlope = value;
                SetPropertiesChanged();
            }
        }
        public float NewSlope {
            get => m_NewSlope;
            set {
                if (value.Equals(m_NewSlope))
                {
                    return;
                }

                m_NewSlope = value;
                SetPropertiesChanged();
            }
        }

        protected IWriter<float> valueWriter {
            get {
                if (m_SlopeWriter == null) {
                    m_SlopeWriter = ValueWriters.Create<float>();
                }

                return m_SlopeWriter;
            }
            set => m_SlopeWriter = value;
        }

        public override string propertiesTypeName => "NT.SlopeTooltip";

        protected override void WriteProperties(IJsonWriter writer) {
            base.WriteProperties(writer);
            writer.PropertyName("CurrentSlope");
            valueWriter.Write(writer, CurrentSlope);
            writer.PropertyName("NewSlope");
            valueWriter.Write(writer, NewSlope);
        }
    }
}