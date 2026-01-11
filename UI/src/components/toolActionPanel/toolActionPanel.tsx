import React, { useState } from "react";
import styles from "./toolActionPanel.module.scss";
import panels from "../shared/panels.module.scss";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { VC, VF, VT } from "components/vanilla/Components";

import { c } from "utils/classes";

const PRESETS = [
    { label: "Linear", id: "linear", icon: "coui://nt/Presets/Slope/Linear.svg" },
    { label: "Eased", id: "easeinout", icon: "coui://nt/Presets/Slope/Eased.svg" },
    { label: "Parabolic", id: "parabolic", icon: "coui://nt/Presets/Slope/Parabolic.svg" },
];

export const ToolActionPanel = () => {
    const selectedBinding = useValue(GAME_BINDINGS.SELECTED_PREFAB.binding);
    const selectedEntitiesBinding = useValue(GAME_BINDINGS.SELECTED_ENTITIES.binding);
    const toolUIDataBinding = useValue(GAME_BINDINGS.UI_DATA.binding);
    const activeIndex = toolUIDataBinding.findIndex((t) => t.ID === selectedBinding);
    const [activePreset, setActivePreset] = useState(PRESETS[0].id);

    return (
        <div className={styles.wrapper}>
            {activeIndex !== -1 && (
                <div className={[panels.nt_panel, styles.panel].join(" ")} key={selectedBinding}>
                    <div className={styles.row}>
                        <span className={styles.toolTitle}>{selectedBinding}</span>
                    </div>
                    {/* Node Selection */}
                    <div className={styles.divider}></div>
                    <div className={styles.col}>
                        {selectedEntitiesBinding.length == 0 && (
                            <span className={styles.helper}>Select two nodes.</span>
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
                                    <span className={styles.label}>Presets</span>
                                    <div className={styles.buttonRow}>
                                        {PRESETS.map((preset) => (
                                            <Button
                                                key={preset.id}
                                                variant="primary"
                                                className={c(
                                                    styles.iconButton,
                                                    activePreset === preset.id
                                                        ? styles.iconButton__active
                                                        : null,
                                                )}
                                                tooltipLabel={preset.label}
                                                onSelect={() => setActivePreset(preset.id)}>
                                                <img src={preset.icon} className={styles.icon} />
                                            </Button>
                                        ))}
                                    </div>
                                </div>
                            </div>
                        </>
                    )}
                    {/* Primary Controls */}
                    <div className={styles.divider}></div>
                    <div className={styles.row}>
                        <div className={styles.actions}>
                            {/* <Button variant="primary" className={styles.iconButton}>
                                <img src={iconsUndo} className={styles.icon} />
                            </Button> */}

                            <Button
                                variant="primary"
                                className={styles.applyButton}
                                disabled={selectedEntitiesBinding.length < 2 || !activePreset}
                                onSelect={() => GAME_TRIGGERS.APPLY_SLOPE(activePreset)}>
                                Apply
                            </Button>
                            {/*
                            <Button variant="primary" className={styles.iconButton}>
                                <img src={iconsRedo} className={styles.icon} />
                            </Button> */}
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};
