namespace NetworkTools.Systems.Tools {
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;

    /// <summary>
    ///     Job definitions for <see cref="NT_GridToolSystem"/>.
    /// </summary>
    public partial class NT_GridToolSystem {
#if BURST
        [BurstCompile]
#endif
        internal struct CreateDefinitionsJob : IJob {
            [ReadOnly] public required GridConfig    Config;
            [ReadOnly] public required ToolOutputMode OutputMode;
            [ReadOnly] public required Entity        PrefabEntity;

            public required EntityCommandBuffer ECB;

            public void Execute() {
                // TODO: Implement grid generation logic
            }
        }
    }
}
