namespace NetworkTools.Systems.Tools.Generate {
    using NetworkTools.Components.Handles;
    using NetworkTools.Systems.Tools.Base;
    using Unity.Collections;

    public struct GridGenerator : IGenerator, IHandleableGenerator {
        public void InitializeConfig(in GenerateMode mode, ref GenerateConfig config) {
        }

        public void GeneratePreview(
            in  GenerateMode         mode,
            in  GenerateConfig       config,
            ref NativeList<CurveDef> curves) {
        }

        public void GenerateNetwork(
            in  GenerateMode         mode,
            in  GenerateConfig       config,
            ref NativeList<CurveDef> curves) {
        }

        public TransformHandleDefinition[] GetHandleDefinitions(
            in GenerateMode   mode,
            in GenerateConfig config) {
            return new[] {
                new TransformHandleDefinition {
                    Key       = HandleKeys.StartPosition,
                    TypeFlags = HandleTypeFlags.Position,
                    Position  = config.StartPosition,
                    Radius    = NT_Handle.PrimaryRadius
                }
            };
        }
    }
}