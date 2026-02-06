import React from "react";
import styles from "./toolActionPanel.module.scss";
import { GAME_BINDINGS, GAME_TRIGGERS, SlopeConfigData } from "gameBindings";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { VC, VF, VT } from "components/vanilla/Components";
import { c } from "utils/classes";

const MODES = [
    { label: "Linear", id: "linear", icon: "coui://nt/Presets/Slope/Linear.svg" },
    { label: "Eased", id: "easeinout", icon: "coui://nt/Presets/Slope/Eased.svg" },
    // { label: "Parabolic", id: "parabolic", icon: "coui://nt/Presets/Slope/Parabolic.svg" },
];

interface SlopeProps {
    toolId?: string;
}

export const Slope: React.FC<SlopeProps> = () => {
    const selectedEntitiesBinding = useValue(GAME_BINDINGS.SELECTED_ENTITIES.binding);
    const slopeConfig = useValue(GAME_BINDINGS.SLOPE_CONFIG.binding);

    const handleParameterChange = (param: keyof SlopeConfigData, value: string | number) => {
        const newConfig: SlopeConfigData = {
            ...slopeConfig,
            [param]: value,
        };
        GAME_BINDINGS.SLOPE_CONFIG.set(newConfig);
    };

    return (
        <>
            {/* Node Selection */}
            <div className={styles.divider}></div>
            <div className={styles.col}>
                {selectedEntitiesBinding.length == 0 && (
                    <span className={styles.helper}>No nodes selected.</span>
                )}
                {selectedEntitiesBinding.length > 0 && (
                    <div>
                        {selectedEntitiesBinding.map((s, i) => (
                            <div key={i} className={styles.selectedEntity}>
                                {s.Name}
                                <VC.ToolButton
                                    src={"Media/Game/Icons/MapMarker.svg"}
                                    onSelect={() => VC.focusEntity(s.Entity)}
                                    multiSelect={false}
                                    className={VT.toolButton.button}
                                    focusKey={VF.FOCUS_DISABLED}
                                    tooltip={"Focus on Entity"}
                                />
                            </div>
                        ))}
                    </div>
                )}
            </div>
            {/* Extra Controls */}
            {selectedEntitiesBinding.length >= 2 && (
                <>
                    <div className={styles.divider}></div>
                    <div className={styles.col}>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>Mode</span>
                                <div className={styles.buttonRow}>
                                    {MODES.map((preset) => (
                                        <Button
                                            key={preset.id}
                                            variant="primary"
                                            className={c(
                                                styles.iconButton,
                                                slopeConfig.template === preset.id
                                                    ? styles.iconButton__active
                                                    : null,
                                            )}
                                            tooltipLabel={preset.label}
                                            onSelect={() =>
                                                handleParameterChange("template", preset.id)
                                            }>
                                            <img src={preset.icon} className={styles.icon} />
                                        </Button>
                                    ))}
                                </div>
                            </div>
                        </div>
                    </div>
                    {slopeConfig.template && (
                        <>
                            {/* EaseInOut Parameters */}
                            {slopeConfig.template === "easeinout" && (
                                <>
                                    <div className={styles.sliderField}>
                                        <VC.FloatSliderField
                                            value={slopeConfig.easeInLength}
                                            label={"Start easing strength"}
                                            min={0}
                                            max={0.5}
                                            fractionDigits={3}
                                            onChange={(e: number) => {
                                                handleParameterChange("easeInLength", e);
                                            }}
                                        />
                                    </div>
                                    <div className={styles.sliderField}>
                                        <VC.FloatSliderField
                                            value={slopeConfig.easeOutLength}
                                            label={"End easing strength"}
                                            min={0}
                                            max={0.5}
                                            fractionDigits={3}
                                            onChange={(e: number) => {
                                                handleParameterChange("easeOutLength", e);
                                            }}
                                        />
                                    </div>
                                </>
                            )}

                            {/* Parabolic Parameters */}
                            {slopeConfig.template === "parabolic" && (
                                <>
                                    <div className={styles.controlRow}>
                                        <VC.FloatSliderField
                                            value={slopeConfig.archHeight}
                                            label={"Arch Height"}
                                            min={-1}
                                            max={1}
                                            fractionDigits={3}
                                            onChange={(e: number) => {
                                                handleParameterChange("archHeight", e);
                                            }}
                                        />
                                    </div>
                                    <div className={styles.controlRow}>
                                        <VC.FloatSliderField
                                            value={slopeConfig.archPosition}
                                            label={"Arch Position"}
                                            min={0.1}
                                            max={0.9}
                                            fractionDigits={3}
                                            onChange={(e: number) => {
                                                handleParameterChange("archPosition", e);
                                            }}
                                        />
                                    </div>
                                </>
                            )}
                        </>
                    )}
                </>
            )}
            {/* Primary Controls */}
            <div className={styles.divider}></div>
            <div className={styles.row}>
                <div className={styles.actions}>
                    {selectedEntitiesBinding.length < 2 && (
                        <span className={styles.helper}>Select at least two nodes.</span>
                    )}
                    {selectedEntitiesBinding.length >= 2 && (
                        <Button
                            variant="primary"
                            className={styles.applyButton}
                            disabled={selectedEntitiesBinding.length < 2 || !slopeConfig.template}
                            onSelect={() => GAME_TRIGGERS.APPLY_SLOPE(slopeConfig.template)}>
                            Apply Slope
                        </Button>
                    )}
                </div>
            </div>
        </>
    );
};
