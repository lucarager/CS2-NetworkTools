import React from "react";
import styles from "../toolActionPanel.module.scss";
import { GAME_TRIGGERS } from "gameBindings";
import {
    GenerateMode,
    PARAM_META,
    PARAM_BINDINGS,
} from "generated/parameters.generated";
import { useValue } from "cs2/api";
import { Button, Tooltip } from "cs2/ui";
import { VC } from "components/vanilla/Components";
import { PrefabSelection } from "../shared/prefabSelection";
import { c } from "utils/classes";
import { useLocalization } from "cs2/l10n";

const G = PARAM_BINDINGS.generate;
const xSpacingMeta = PARAM_META["generate.gridXSpacing"];
const zSpacingMeta = PARAM_META["generate.gridZSpacing"];
const xNumMeta = PARAM_META["generate.gridXNum"];
const zNumMeta = PARAM_META["generate.gridZNum"];

const GENERATE_MODES: { localeKey: string; id: GenerateMode; icon: string }[] = [
    {
        localeKey: "NetworkTools.UI.Generate.Grid",
        id: GenerateMode.Grid,
        icon: "coui://nt/Modes/GenerateGrid.svg",
    },
    {
        localeKey: "NetworkTools.UI.Generate.Circle",
        id: GenerateMode.Circle,
        icon: "coui://nt/Modes/GenerateCircle.svg",
    },
];

export const GenerateControls: React.FC = () => {
    const activeGenerateMode = useValue(G.mode.binding) as GenerateMode;
    const gridXSpacing = useValue(G.gridXSpacing.binding);
    const gridZSpacing = useValue(G.gridZSpacing.binding);
    const gridXNum = useValue(G.gridXNum.binding);
    const gridZNum = useValue(G.gridZNum.binding);
    const { translate } = useLocalization();

    return (
        <>
            <PrefabSelection />
            <div className={styles.divider}></div>
            <div className={styles.col}>
                <div className={styles.controlRow}>
                    <div className={styles.controlRowInner}>
                        <span className={styles.paramLabel}>
                            {translate("NetworkTools.UI.Common.Mode")}
                        </span>
                        <div className={styles.buttonRow}>
                            {GENERATE_MODES.map((mode) => (
                                <Tooltip
                                    key={mode.id}
                                    tooltip={translate(mode.localeKey)}
                                    delayTime={0}>
                                    <Button
                                        key={mode.id}
                                        variant="primary"
                                        className={c(
                                            styles.iconButton,
                                            activeGenerateMode === mode.id
                                                ? styles.iconButton__active
                                                : null,
                                        )}
                                        onSelect={() => G.mode.set(mode.id)}>
                                        <img src={mode.icon} className={styles.icon} />
                                    </Button>
                                </Tooltip>
                            ))}
                        </div>
                    </div>
                </div>
            </div>
            <div className={styles.divider}></div>
            <div className={styles.col}>
                <div className={styles.controlRow}>
                    <div className={styles.sliderField}>
                        <VC.FloatSliderField
                            value={gridXSpacing}
                            label={translate("NetworkTools.UI.Generate.XSpacing") ?? ""}
                            min={xSpacingMeta.min}
                            max={xSpacingMeta.max}
                            fractionDigits={0}
                            onChange={(e: number) => G.gridXSpacing.set(e)}
                        />
                    </div>
                </div>
                <div className={styles.controlRow}>
                    <div className={styles.sliderField}>
                        <VC.FloatSliderField
                            value={gridZSpacing}
                            label={translate("NetworkTools.UI.Generate.ZSpacing") ?? ""}
                            min={zSpacingMeta.min}
                            max={zSpacingMeta.max}
                            fractionDigits={0}
                            onChange={(e: number) => G.gridZSpacing.set(e)}
                        />
                    </div>
                </div>
                <div className={styles.controlRow}>
                    <div className={styles.sliderField}>
                        <VC.FloatSliderField
                            value={gridXNum}
                            label={translate("NetworkTools.UI.Generate.XCount") ?? ""}
                            min={xNumMeta.min}
                            max={xNumMeta.max}
                            fractionDigits={0}
                            onChange={(e: number) => G.gridXNum.set(Math.round(e))}
                        />
                    </div>
                </div>
                <div className={styles.controlRow}>
                    <div className={styles.sliderField}>
                        <VC.FloatSliderField
                            value={gridZNum}
                            label={translate("NetworkTools.UI.Generate.ZCount") ?? ""}
                            min={zNumMeta.min}
                            max={zNumMeta.max}
                            fractionDigits={0}
                            onChange={(e: number) => G.gridZNum.set(Math.round(e))}
                        />
                    </div>
                </div>
            </div>

            {/* Apply Button */}
            <div className={styles.divider}></div>
            <div className={styles.row}>
                <div className={styles.actions}>
                    <Button
                        variant="primary"
                        className={styles.applyButton}
                        onSelect={() => GAME_TRIGGERS.REQUEST_APPLY()}>
                        {translate("NetworkTools.UI.Generate.CreateGrid")}
                    </Button>
                </div>
            </div>
        </>
    );
};
