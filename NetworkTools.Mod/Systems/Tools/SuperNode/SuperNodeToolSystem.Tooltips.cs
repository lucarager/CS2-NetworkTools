namespace NetworkTools.Systems.Tools {
    using System.Collections.Generic;
    using Game.Common;
    using Game.Input;
    using Game.Notifications;
    using Game.Net;
    using Game.Prefabs;
    using Game.Tools;

    using NetworkTools.Components.Handles;
    using NetworkTools.Components;
    using NetworkTools.Components.Tools;

    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Jobs;
    using Unity.Collections;

    public partial class NT_SuperNodeToolSystem {
        public override IReadOnlyList<HintTooltipEntry> GetHintTooltips(
            OperationPhase phase,
            ProxyAction applyAction,
            ProxyAction secondaryApplyAction) {
            return phase switch {
                OperationPhase.Idle => new HintTooltipEntry[] {
                    new("NetworkTools.HintTooltip.SuperNode.SelectStart", applyAction),
                    new("NetworkTools.HintTooltip.Common.Exit", secondaryApplyAction)
                },
                OperationPhase.Configuring => new HintTooltipEntry[] {
                    new("NetworkTools.HintTooltip.SuperNode.SelectSecond", applyAction),
                    new("NetworkTools.HintTooltip.SuperNode.RemoveLast", secondaryApplyAction)
                },
                OperationPhase.Ready => new HintTooltipEntry[] {
                    new("NetworkTools.HintTooltip.SuperNode.SelectSecond", applyAction),
                    new("NetworkTools.HintTooltip.SuperNode.RemoveLast", secondaryApplyAction)
                },
                _ => System.Array.Empty<HintTooltipEntry>()
            };
        }
    }
}