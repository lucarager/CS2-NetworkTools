import React from "react";
import styles from "./toolActionPanel.module.scss";
import panels from "../shared/panels.module.scss";
import { GAME_BINDINGS } from "gameBindings";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { VC, VF, VT } from "components/vanilla/Components";

export const ToolActionPanel = () => {
    const selectedBinding = useValue(GAME_BINDINGS.SELECTED_PREFAB.binding);
    const selectedEntitiesBinding = useValue(GAME_BINDINGS.SELECTED_ENTITIES.binding);
    const toolUIDataBinding = useValue(GAME_BINDINGS.UI_DATA.binding);
    const activeIndex = toolUIDataBinding.findIndex((t) => t.ID === selectedBinding);

    return (
        <div className={styles.wrapper}>
            {activeIndex !== -1 && (
                <div className={[panels.nt_panel, styles.panel].join(" ")} key={selectedBinding}>
                    <div className={styles.row}>
                        <span className={styles.toolTitle}>{selectedBinding}</span>
                        <div className={styles.actions}>
                            {/* <Button variant="primary" className={styles.iconButton}>
                                <img src={iconsUndo} className={styles.icon} />
                            </Button>
                            <Button variant="primary" className={styles.applyButton}>
                                Apply
                            </Button>
                            <Button variant="primary" className={styles.iconButton}>
                                <img src={iconsRedo} className={styles.icon} />
                            </Button> */}
                        </div>
                    </div>
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
                </div>
            )}
        </div>
    );
};
