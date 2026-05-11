namespace NetworkTools.Systems.Tools.Parallel {
    using System.Collections.Generic;

    using Game.Input;
    using NetworkTools.Systems.Tools;

    public partial class NT_ParallelToolSystem {
        public override IReadOnlyList<HintTooltipEntry> GetHintTooltips(
            OperationPhase phase,
            ProxyAction applyAction,
            ProxyAction secondaryApplyAction) {
            return phase switch {
                OperationPhase.Idle => new HintTooltipEntry[] {
                    new("NetworkTools.HintTooltip.Parallel.SelectStart", applyAction),
                    new("NetworkTools.HintTooltip.Common.Exit", secondaryApplyAction)
                },
                OperationPhase.Configuring => new HintTooltipEntry[] {
                    new("NetworkTools.HintTooltip.Parallel.SelectSecond", applyAction),
                    new("NetworkTools.HintTooltip.Parallel.RemoveLast", secondaryApplyAction)
                },
                OperationPhase.Ready => new HintTooltipEntry[] {
                    new("NetworkTools.HintTooltip.Parallel.ExtendPath", applyAction),
                    new("NetworkTools.HintTooltip.Parallel.RemoveLast", secondaryApplyAction)
                },
                _ => System.Array.Empty<HintTooltipEntry>()
            };
        }
    }
}
