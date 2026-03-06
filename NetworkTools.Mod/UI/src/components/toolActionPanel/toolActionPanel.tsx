import React from "react";
import styles from "./toolActionPanel.module.scss";
import panels from "../shared/panels.module.scss";
import { GAME_BINDINGS } from "gameBindings";
import { useValue } from "cs2/api";
import { ShapeSlopeControls } from "./shapeSlope";
import { ShapeCurveControls } from "./shapeCurve";
import { ConnectControls } from "./connect";

// Registry of tool components mapped by tool ID
const TOOL_COMPONENTS: Record<string, React.ComponentType<any>> = {
    shape_slope: ShapeSlopeControls,
    shape_curve: ShapeCurveControls,
    connect: ConnectControls,
};

export const ToolActionPanel = () => {
    const selectedBinding = useValue(GAME_BINDINGS.SELECTED_PREFAB.binding);
    const toolUIDataBinding = useValue(GAME_BINDINGS.UI_DATA.binding);
    const activeTool = toolUIDataBinding.find((t) => t.PrefabId === selectedBinding);

    console.log("Active Tool ID:", activeTool);

    if (!activeTool) {
        return <div className={styles.wrapper}></div>;
    }

    console.log("Rendering ToolActionPanel for tool:", selectedBinding);

    const ToolComponent = TOOL_COMPONENTS[activeTool.Id];

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
