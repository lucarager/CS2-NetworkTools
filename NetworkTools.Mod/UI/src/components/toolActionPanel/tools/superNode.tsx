import React from "react";
import styles from "../toolActionPanel.module.scss";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { NodeSelection } from "../shared/nodeSelection";
import { useLocalization } from "cs2/l10n";

export const SuperNodeControls: React.FC = () => {
    const selectedEntitiesBinding = useValue(GAME_BINDINGS.SELECTED_ENTITIES.binding);
    const { translate } = useLocalization();

    console.log("Selected Entities in SuperNodeControls:", selectedEntitiesBinding);

    return (
        <>
            <NodeSelection selectedEntities={selectedEntitiesBinding} />

            {/* Primary Controls */}
            <div className={styles.divider}></div>
            <div className={styles.row}>
                <div className={styles.actions}>
                    {selectedEntitiesBinding.length < 2 && (
                        <span className={styles.helper}>{translate("NetworkTools.UI.Common.SelectAtLeastTwoNodes")}</span>
                    )}
                    {selectedEntitiesBinding.length >= 2 && (
                        <Button
                            variant="primary"
                            className={styles.applyButton}
                            disabled={selectedEntitiesBinding.length < 2}
                            onSelect={() => GAME_TRIGGERS.REQUEST_APPLY()}>
                            {translate("NetworkTools.UI.SuperNode.CreateSupernode")}
                        </Button>
                    )}
                </div>
            </div>
        </>
    );
};
