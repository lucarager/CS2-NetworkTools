namespace NetworkTools.Systems.Tools.RoadShape {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Colossal.Mathematics;

    public static class SlopeUtils {
        public static float GetHeightAtCurvePosition(Bezier4x3 curve, float t) {
            return MathUtils.Position(curve, t).y;
        }
    }
}
