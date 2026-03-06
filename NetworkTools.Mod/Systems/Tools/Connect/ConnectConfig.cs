namespace NetworkTools.Systems.Tools.Connect {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Colossal.UI.Binding;

    /// <summary>
    /// Struct to hold connection parameters and their values.
    /// </summary>
    public struct ConnectConfig : IJsonWritable, IJsonReadable {
        public static ConnectConfig Default() {
            return new ConnectConfig {
            };
        }

        /// <inheritdoc/>
        public void Write(IJsonWriter writer) {
            writer.TypeBegin(GetType().FullName);
            writer.TypeEnd();
        }

        /// <inheritdoc/>
        public void Read(IJsonReader reader) {
            reader.ReadMapBegin();
            reader.ReadMapEnd();
        }
    }
}
