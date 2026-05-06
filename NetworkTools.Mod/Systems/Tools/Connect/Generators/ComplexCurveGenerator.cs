namespace NetworkTools.Systems.Tools.Connect {
    using System.Collections.Generic;

    using Colossal.Mathematics;

    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Base;
    using NetworkTools.Systems.Tools.Parameters;

    using Unity.Collections;
    using Unity.Mathematics;

    public struct ComplexCurveGenerator : IConnectionGenerator {
        public void InitializeConfig(ref ConnectJobConfig config) {
            // Not implemented yet
        }

        public void GenerateConnection(
            in  ConnectJobConfig     config,
            ref NativeList<CurveDef> curves) {
            // Not implemented yet
        }

        public static TransformHandleDefinition[] BuildHandleDefinitions(
            in ConnectJobConfig config,
            IReadOnlyDictionary<string, ParameterBase> parameters) {

            return null;
        }
    }
}
