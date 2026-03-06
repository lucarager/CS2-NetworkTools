namespace NetworkTools.Systems.Tools.Connect {
    using Colossal.Entities;

    using Game.Common;
    using Game.Net;
    using Game.Notifications;
    using Game.Prefabs;
    using Game.Prefabs;
    using Game.Rendering;
    using Game.Simulation;
    using Game.Tools;

    using NetworkTools.Components;
    using NetworkTools.Components;
    using NetworkTools.Settings;
    using NetworkTools.Systems.Tools.RoadShape;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Mathematics;

    public partial class NT_ConnectToolSystem {
#if BURST
        [BurstCompile]
#endif
        internal struct CreateDefinitionsJob : IJob {
            [ReadOnly] public required ConnectMode   Mode;
            [ReadOnly] public required ConnectConfig Config;
            [ReadOnly] public required ToolOutputMode OutputMode;
            [ReadOnly] public required NativeList<Entity> SelectedNodeEntities;
            [ReadOnly] public required Entity PrefabEntity;

            [ReadOnly] public required ComponentLookup<Node>             NodeLookup;
            [ReadOnly] public required ComponentLookup<PrefabRef>        PrefabRefLookup;
            [ReadOnly] public required ComponentLookup<PseudoRandomSeed> PseudoRandomSeedLookup;
            [ReadOnly] public required BufferLookup<ConnectedEdge>       ConnectedEdgeLookup;
            [ReadOnly] public required ComponentLookup<Edge>             EdgeLookup;
            [ReadOnly] public required ComponentLookup<Curve>            CurveLookup;
            [ReadOnly] public required ComponentLookup<Upgraded>         UpgradedLookup;
            [ReadOnly] public required ComponentLookup<Aggregated>       AggregatedLookup;

            public required            EntityCommandBuffer               ECB;

            public void Execute() {
            }
        }
    }
}
