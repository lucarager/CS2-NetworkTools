namespace NetworkTools.Systems.Tools.Parameters {
    using System;

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class EnumOptionAttribute : Attribute {
        public string Label { get; }
        public string Icon  { get; }
        public string Group { get; set; }
        public bool Visible { get; set; } = true;
        public bool Disabled { get; set; }

        public EnumOptionAttribute(string label, string icon) {
            Label = label;
            Icon  = icon;
        }
    }
}
