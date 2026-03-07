namespace NetworkTools.Systems.Tools.Connect {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    ///     Defines the type of transformation to apply
    /// </summary>
    public enum ConnectMode {
        None = 0,
        SimpleCurve = 1,
        ComplexCurve = 2,
        Loop = 3,
    }
}
