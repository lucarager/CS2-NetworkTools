import React from "react";
import styles from "./toolActionPanel.module.scss";
import { Button } from "cs2/ui";
import iconsUndo from "assets/icons/undo.svg";
import iconsRedo from "assets/icons/redo.svg";
import panels from "../shared/panels.module.scss";
import { GAME_BINDINGS } from "gameBindings";
import { useValue } from "cs2/api";

export const ToolActionPanel = () => {
    const selectedBinding = useValue(GAME_BINDINGS.SELECTED_PREFAB.binding);
    const toolUIDataBinding = useValue(GAME_BINDINGS.UI_DATA.binding);
    const activeIndex = toolUIDataBinding.findIndex((t) => t.ID === selectedBinding);

    return (
        <div className={styles.wrapper}>
            {activeIndex !== -1 && (
                <div className={[panels.nt_panel, styles.panel].join(" ")} key={selectedBinding}>
                    <div className={styles.row}>
                        <span className={styles.toolTitle}>{selectedBinding}</span>
                        <div className={styles.actions}>
                            <Button variant="primary" className={styles.iconButton}>
                                <img src={iconsUndo} className={styles.icon} />
                            </Button>
                            <Button variant="primary" className={styles.applyButton}>
                                Apply
                            </Button>
                            <Button variant="primary" className={styles.iconButton}>
                                <img src={iconsRedo} className={styles.icon} />
                            </Button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};
