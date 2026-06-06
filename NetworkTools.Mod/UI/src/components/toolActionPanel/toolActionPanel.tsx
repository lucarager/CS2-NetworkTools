import React, { useState, useEffect } from "react";
import styles from "./toolActionPanel.module.scss";
import panels from "../shared/panels.module.scss";
import { GAME_BINDINGS, GAME_TRIGGERS, ApplyState } from "gameBindings";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { ShapeSlopeControls } from "./tools/shapeSlope";
import { ShapeCurveControls } from "./tools/shapeCurve";
import { ConnectControls } from "./tools/connect";
import { useLocalization } from "cs2/l10n";
import { SnapSelection } from "./shared/snapSelection";
import { TargetSelection } from "./shared/targetSelection";
import { AnarchySelection } from "./shared/anarchySelection";
import { ViewSelection } from "./shared/viewSelection";
import { SuperNodeControls } from "./tools/superNode";
import { ParallelControls } from "./tools/parallel";
import { GenerateControls } from "./tools/generate";
import { PrefabSearchProvider, usePrefabSearch } from "./prefabSearchPanel/prefabSearchContext";
import { PrefabSearchPanel } from "./prefabSearchPanel/prefabSearchPanel";

// Maps a tool's Id to its parameter-controls component. Whether the Apply button
// shows / is enabled is decided entirely on the C# side via the APPLY_STATE binding.
const TOOL_COMPONENTS: Record<string, React.ComponentType<any>> = {
    ShapeSlope: ShapeSlopeControls,
    ShapeCurve: ShapeCurveControls,
    Connect: ConnectControls,
    SuperNode: SuperNodeControls,
    Parallel: ParallelControls,
    Generate: GenerateControls,
};

const ToolActionPanelInner = () => {
    const selectedBinding = useValue(GAME_BINDINGS.SELECTED_PREFAB.binding);
    const toolUIDataBinding = useValue(GAME_BINDINGS.UI_DATA.binding);
    const applyState = useValue(GAME_BINDINGS.APPLY_STATE.binding) as ApplyState;
    const activeTool = toolUIDataBinding.find((t) => t.PrefabId === selectedBinding);
    const { translate } = useLocalization();
    const [showTutorial, setShowTutorial] = useState(false);
    const { isOpen: prefabSearchOpen, close: closePrefabSearch } = usePrefabSearch();

    useEffect(() => {
        setShowTutorial(false);
        closePrefabSearch();
    }, [closePrefabSearch, selectedBinding]);

    if (!activeTool) {
        return <div className={styles.wrapper}></div>;
    }

    const ToolComponent = TOOL_COMPONENTS[activeTool.Id];

    return (
        <div className={styles.wrapper}>
            <div className={[panels.nt_panel, styles.panel].join(" ")} key={selectedBinding}>
                <div className={panels.nt_panel__header}>
                    <div className={styles.titleBlock}>
                        <span className={styles.toolTitle}>
                            {translate(activeTool.DisplayName)}
                        </span>
                        <span className={styles.toolDescription}>
                            {translate(activeTool.Description)}
                        </span>
                    </div>
                    {/* <Button
                        variant="icon"
                        tooltipLabel={translate("NetworkTools.UI.Common.HowToUse")}
                        className={c(styles.infoButton, showTutorial && styles.infoButtonActive)}
                        onSelect={() => setShowTutorial((v) => !v)}>
                            <VC.TintedIcon src={"Media/Glyphs/Info.svg"} className={styles.icon} />
                    </Button> */}
                </div>
                <div className={panels.nt_panel__content}>
                    <div className={styles.section}>
                        <div className={styles.section__content}>
                            <ViewSelection />
                            <TargetSelection />
                            <SnapSelection />
                            <AnarchySelection />
                        </div>
                    </div>
                    {ToolComponent && <ToolComponent />}
                    {applyState !== ApplyState.Hidden && (
                        <div className={styles.row}>
                            <div className={styles.actions}>
                                {applyState === ApplyState.InsufficientNodes ? (
                                    <span className={styles.helper}>
                                        {translate("NetworkTools.UI.Common.SelectAtLeastTwoNodes")}
                                    </span>
                                ) : (
                                    <Button
                                        variant="primary"
                                        className={styles.applyButton}
                                        disabled={applyState === ApplyState.Disabled}
                                        onSelect={() => GAME_TRIGGERS.REQUEST_APPLY()}>
                                        {translate(`NetworkTools.UI.Apply.${activeTool.Id}`)}
                                    </Button>
                                )}
                            </div>
                        </div>
                    )}
                </div>
            </div>
            {showTutorial && (
                <div className={[panels.nt_panel, styles.tutorialPanel].join(" ")}>
                    <div className={styles.col}>
                        <span className={styles.tutorialTitle}>
                            {translate("NetworkTools.UI.Common.HowToUse")}
                        </span>
                        <span className={styles.tutorialText}>
                            {translate("NetworkTools.UI.Common.Tutorial")}
                        </span>
                    </div>
                </div>
            )}
            {prefabSearchOpen && <PrefabSearchPanel onClose={closePrefabSearch} />}
        </div>
    );
};

export const ToolActionPanel = () => (
    <PrefabSearchProvider>
        <ToolActionPanelInner />
    </PrefabSearchProvider>
);
