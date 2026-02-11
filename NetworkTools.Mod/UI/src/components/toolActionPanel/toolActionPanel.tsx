import React from "react";
import styles from "./toolActionPanel.module.scss";
import panels from "../shared/panels.module.scss";
import { GAME_BINDINGS } from "gameBindings";
import { useValue } from "cs2/api";
import { Slope } from "./slope";

// Registry of tool components mapped by tool ID
const TOOL_COMPONENTS: Record<string, React.ComponentType<any>> = {
    "Path Shape Tools": Slope,
};

export const ToolActionPanel = () => {
    const selectedBinding = useValue(GAME_BINDINGS.SELECTED_PREFAB.binding);
    const toolUIDataBinding = useValue(GAME_BINDINGS.UI_DATA.binding);
    const activeIndex = toolUIDataBinding.findIndex((t) => t.ID === selectedBinding);

    if (activeIndex === -1) {
        return <div className={styles.wrapper}></div>;
    }

    console.log("Rendering ToolActionPanel for tool:", selectedBinding);

    const ToolComponent = TOOL_COMPONENTS[selectedBinding];

    return (
        <div className={styles.wrapper}>
            <div className={[panels.nt_panel, styles.panel].join(" ")} key={selectedBinding}>
                <div className={styles.row}>
                    <span className={styles.toolTitle}>{selectedBinding}</span>
                </div>
                {ToolComponent && <ToolComponent toolId={selectedBinding} />}
            </div>
        </div>
    );
};
