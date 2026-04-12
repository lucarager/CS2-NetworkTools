import React from "react";
import styles from "../toolActionPanel.module.scss";
import { GAME_BINDINGS, GAME_TRIGGERS, GridConfigData } from "gameBindings";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { VC } from "components/vanilla/Components";

export const GridControls: React.FC = () => {
    const gridConfig = useValue(GAME_BINDINGS.GRID_CONFIG.binding);
    const selectedNetPrefab = useValue(GAME_BINDINGS.SELECTED_NET_PREFAB.binding);

    const handleConfigChange = (param: keyof GridConfigData, value: number) => {
        const newConfig: GridConfigData = {
            ...gridConfig,
            [param]: value,
        };

        GAME_BINDINGS.GRID_CONFIG.set(newConfig);
    };

    return (
        <>
            <div className={styles.divider}></div>
            <div className={styles.col}>
                <div className={styles.controlRow}>
                    <div className={styles.controlRowInner}>
                        <span className={styles.paramLabel}>Network Prefab</span>
                        <div className={styles.entityPreview}>
                            <img
                                src={selectedNetPrefab.Thumbnail}
                                className={styles.entityPreview__thumbnail}
                            />
                            <span className={styles.entityPreview__name}>
                                {selectedNetPrefab.Name}
                            </span>
                        </div>
                    </div>
                </div>
                <div className={styles.controlRow}>
                    <div className={styles.sliderField}>
                        <VC.FloatSliderField
                            value={gridConfig.angle}
                            label={"Angle"}
                            min={-180}
                            max={180}
                            fractionDigits={1}
                            onChange={(e: number) => handleConfigChange("angle", e)}
                        />
                    </div>
                </div>
                <div className={styles.controlRow}>
                    <div className={styles.sliderField}>
                        <VC.FloatSliderField
                            value={gridConfig.xSpacing}
                            label={"X Spacing"}
                            min={4}
                            max={500}
                            fractionDigits={1}
                            onChange={(e: number) => handleConfigChange("xSpacing", e)}
                        />
                    </div>
                </div>
                <div className={styles.controlRow}>
                    <div className={styles.sliderField}>
                        <VC.FloatSliderField
                            value={gridConfig.zSpacing}
                            label={"Z Spacing"}
                            min={4}
                            max={500}
                            fractionDigits={1}
                            onChange={(e: number) => handleConfigChange("zSpacing", e)}
                        />
                    </div>
                </div>
                <div className={styles.controlRow}>
                    <div className={styles.sliderField}>
                        <VC.FloatSliderField
                            value={gridConfig.xNum}
                            label={"X Count"}
                            min={1}
                            max={20}
                            fractionDigits={0}
                            onChange={(e: number) =>
                                handleConfigChange("xNum", Math.round(e))
                            }
                        />
                    </div>
                </div>
                <div className={styles.controlRow}>
                    <div className={styles.sliderField}>
                        <VC.FloatSliderField
                            value={gridConfig.zNum}
                            label={"Z Count"}
                            min={1}
                            max={20}
                            fractionDigits={0}
                            onChange={(e: number) =>
                                handleConfigChange("zNum", Math.round(e))
                            }
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
