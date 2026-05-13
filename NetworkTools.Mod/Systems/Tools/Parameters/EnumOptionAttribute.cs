namespace NetworkTools.Systems.Tools.Parameters {
    using System;

    [AttributeUsage(AttributeTargets.Field)]
    public class EnumOptionAttribute : Attribute {
        public string Label { get; }
        public string Icon  { get; }

        public EnumOptionAttribute(string label, string icon) {
            Label = label;
            Icon  = icon;
        }
    }
}
