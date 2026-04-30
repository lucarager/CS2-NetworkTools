import React from "react";
import styles from "../toolActionPanel.module.scss";
import { GAME_BINDINGS, GAME_TRIGGERS, GenerateConfigData, GenerateMode } from "gameBindings";
import { useValue } from "cs2/api";
import { Button, Tooltip } from "cs2/ui";
import { VC } from "components/vanilla/Components";
import { PrefabSelection } from "../shared/prefabSelection";
import { c } from "utils/classes";

const GENERATE_MODES: { label: string; id: GenerateMode; icon: string }[] = [
    {
        label: "Grid",
        id: GenerateMode.Grid,
        icon: "coui://nt/Modes/Original.svg",
    },
    {
        label: "Circle",
        id: GenerateMode.Circle,
        icon: "coui://nt/Modes/Original.svg",
    },
];

export const GenerateControls: React.FC = () => {
    const gridConfig = useValue(GAME_BINDINGS.GENERATE_CONFIG.binding);
    const activeGenerateMode = useValue(GAME_BINDINGS.GENERATE_MODE.binding);

    const handleConfigChange = (param: keyof GenerateConfigData, value: number) => {
        const newConfig: GenerateConfigData = {
            ...gridConfig,
            [param]: value,
        };

        GAME_BINDINGS.GENERATE_CONFIG.set(newConfig);
    };

    return (
        <>
            <PrefabSelection />
            <div className={styles.divider}></div>
            <div className={styles.col}>
                <div className={styles.controlRow}>
                    <div className={styles.controlRowInner}>
                        <span className={styles.paramLabel}>Mode</span>
                        <div className={styles.buttonRow}>
                            {GENERATE_MODES.map((mode) => (
                                <Tooltip key={mode.id} tooltip={mode.label} delayTime={0}>
                                    <Button
                                        key={mode.id}
                                        variant="primary"
                                        className={c(
                                            styles.iconButton,
                                            activeGenerateMode === mode.id
                                                ? styles.iconButton__active
                                                : null,
                                        )}
                                        onSelect={() => GAME_BINDINGS.GENERATE_MODE.set(mode.id)}>
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
                            value={gridConfig.gridXSpacing}
                            label={"X Spacing"}
                            min={4}
                            max={500}
                            fractionDigits={1}
                            onChange={(e: number) => handleConfigChange("gridXSpacing", e)}
                        />
                    </div>
                </div>
                <div className={styles.controlRow}>
                    <div className={styles.sliderField}>
                        <VC.FloatSliderField
                            value={gridConfig.gridZSpacing}
                            label={"Z Spacing"}
                            min={4}
                            max={500}
                            fractionDigits={1}
                            onChange={(e: number) => handleConfigChange("gridZSpacing", e)}
                        />
                    </div>
                </div>
                <div className={styles.controlRow}>
                    <div className={styles.sliderField}>
                        <VC.FloatSliderField
                            value={gridConfig.gridXNum}
                            label={"X Count"}
                            min={1}
                            max={20}
                            fractionDigits={0}
                            onChange={(e: number) => handleConfigChange("gridXNum", Math.round(e))}
                        />
                    </div>
                </div>
                <div className={styles.controlRow}>
                    <div className={styles.sliderField}>
                        <VC.FloatSliderField
                            value={gridConfig.gridZNum}
                            label={"Z Count"}
                            min={1}
                            max={20}
                            fractionDigits={0}
                            onChange={(e: number) => handleConfigChange("gridZNum", Math.round(e))}
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
                        Create grid network
                    </Button>
                </div>
            </div>
        </>
    );
};
