import React from "react";
import styles from "./toolActionPanel.module.scss";
import { ToolSelectionData } from "gameBindings";
import { VC, VF, VT } from "components/vanilla/Components";

type NodeSelectionProps = {
    selectedEntities: ToolSelectionData[];
};

export const NodeSelection: React.FC<NodeSelectionProps> = ({ selectedEntities }) => {
    return (
        <>
            <div className={styles.divider}></div>
            <div className={styles.col} style={{ minHeight: "80rem" }}>
                {selectedEntities.length > 0 && (
                    <div>
                        {selectedEntities.map((selection, index) => (
                            <div key={index} className={styles.selectedEntity}>
                                <div className={styles.selectedEntity__name}>{selection.Name}</div>
                                <VC.ToolButton
                                    src={"Media/Game/Icons/MapMarker.svg"}
                                    onSelect={() => VC.focusEntity(selection.Entity)}
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
        </>
    );
};
