import React from "react";
import styles from "./toolActionPanel.module.scss";
import panels from "../shared/panels.module.scss";
import { GAME_BINDINGS } from "gameBindings";
import { useValue } from "cs2/api";
import { ShapeSlopeControls } from "./tools/shapeSlope";
import { ShapeCurveControls } from "./tools/shapeCurve";
import { ConnectControls } from "./tools/connect";
import { useLocalization } from "cs2/l10n";
import { SnapSelection } from "./shared/snapSelection";
import { TargetSelection } from "./shared/targetSelection";
import { ViewSelection } from "./shared/viewSelection";
import { SuperNodeControls } from "./tools/superNode";
import { ParallelControls } from "./tools/parallel";

// Registry of tool components mapped by tool ID
const TOOL_COMPONENTS: Record<string, React.ComponentType<any>> = {
    ShapeSlope: ShapeSlopeControls,
    ShapeCurve: ShapeCurveControls,
    Connect: ConnectControls,
    SuperNode: SuperNodeControls,
    Parallel: ParallelControls,
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
                    <ViewSelection />
                    <TargetSelection />
                    <SnapSelection />
                </div>
                {ToolComponent && <ToolComponent toolId={selectedBinding} />}
            </div>
        </div>
    );
};
