namespace NetworkTools.Systems.Tools.Generate {
    using System.Collections.Generic;

    using Game.Input;
    using NetworkTools.Systems.Tools;

    public partial class NT_GenerateToolSystem {
        public override IReadOnlyList<HintTooltipEntry> GetHintTooltips(
            OperationPhase phase,
            ProxyAction applyAction,
            ProxyAction secondaryApplyAction,
            ProxyAction preciseRotationAction) {
            return phase switch {
                OperationPhase.Idle => new HintTooltipEntry[] {
                    new("NetworkTools.HintTooltip.Generate.Place", applyAction),
                    new("NetworkTools.HintTooltip.Generate.Rotate", preciseRotationAction),
                    new("NetworkTools.HintTooltip.Common.Exit", secondaryApplyAction)
                },
                OperationPhase.Ready => new HintTooltipEntry[] {
                    new("NetworkTools.HintTooltip.Generate.Rotate", preciseRotationAction),
                    new("NetworkTools.HintTooltip.Generate.RemovePlacement", secondaryApplyAction)
                },
                _ => System.Array.Empty<HintTooltipEntry>()
            };
        }
    }
}
