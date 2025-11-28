import React from "react";
import styles from "./toolExtraPanel.module.scss";
import panels from "../shared/panels.module.scss";
import { useValue } from "cs2/api";
import { GAME_BINDINGS } from "gameBindings";

export const ToolExtraPanel = () => {
    const selectedBinding = useValue(GAME_BINDINGS.SELECTED_PREFAB.binding);
    const toolUIDataBinding = useValue(GAME_BINDINGS.UI_DATA.binding);
    const activeIndex = toolUIDataBinding.findIndex((t) => t.ID === selectedBinding);

    return (
        <div className={styles.wrapper}>
            {activeIndex !== -1 && (
                <div className={[panels.nt_panel, styles.panel].join(" ")}>
                    <div className={styles.content}>Extra Panel Header</div>
                    <div className={styles.divider}></div>
                    <div className={styles.content}>Extra Panel Content</div>
                </div>
            )}
        </div>
    );
};
