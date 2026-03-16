import React from "react";
import styles from "./toolActionPanel.module.scss";
import panels from "../shared/panels.module.scss";
import { GAME_BINDINGS } from "gameBindings";
import { useValue } from "cs2/api";
import { ShapeSlopeControls } from "./shapeSlope";
import { ShapeCurveControls } from "./shapeCurve";
import { ConnectControls } from "./connect";
import { useLocalization } from "cs2/l10n";
import { SnapSelection } from "./snapSelection";
import { TargetSelection } from "./targetSelection";

// Registry of tool components mapped by tool ID
const TOOL_COMPONENTS: Record<string, React.ComponentType<any>> = {
    ShapeSlope: ShapeSlopeControls,
    ShapeCurve: ShapeCurveControls,
    Connect: ConnectControls,
};

export const ToolActionPanel = () => {
    const selectedBinding = useValue(GAME_BINDINGS.SELECTED_PREFAB.binding);
    const toolUIDataBinding = useValue(GAME_BINDINGS.UI_DATA.binding);
    const activeTool = toolUIDataBinding.find((t) => t.PrefabId === selectedBinding);
    const { translate } = useLocalization();

    if (!activeTool) {
        return <div className={styles.wrapper}></div>;
    }

    const ToolComponent = TOOL_COMPONENTS[activeTool.Id];

    return (
        <div className={styles.wrapper}>
            <div className={[panels.nt_panel, styles.panel].join(" ")} key={selectedBinding}>
                <div className={styles.col}>
                    <span className={styles.toolTitle}>{translate(activeTool.DisplayName)}</span>
                    <span className={styles.toolDescription}>
                        {translate(activeTool.Description)}
                    </span>
                </div>
                <div className={styles.col}>
                    <SnapSelection />
                    <TargetSelection />
                </div>
                {ToolComponent && <ToolComponent toolId={selectedBinding} />}
            </div>
        </div>
    );
};
