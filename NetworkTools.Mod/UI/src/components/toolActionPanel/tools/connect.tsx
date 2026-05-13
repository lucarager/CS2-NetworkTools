import React from "react";
import styles from "../toolActionPanel.module.scss";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import { ConnectMode, PARAM_KEYS, PARAM_BINDINGS } from "generated/parameters.generated";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { PrefabSelection } from "../shared/prefabSelection";
import { ParameterField } from "../shared/parameterField";
import { useLocalization } from "cs2/l10n";

const C = PARAM_BINDINGS.connect;

export const ConnectControls: React.FC = () => {
    const selectedEntitiesBinding = useValue(GAME_BINDINGS.SELECTED_ENTITIES.binding);
    const activeConnectMode = useValue(C.mode.binding) as ConnectMode;
    const { translate } = useLocalization();

    return (
        <>
            {/* <NodeSelection selectedEntities={selectedEntitiesBinding} /> */}

            {/* Transform Controls - Show when 2+ nodes selected */}
            {selectedEntitiesBinding.length >= 2 && (
                <>
                    <div className={styles.divider}></div>
                    <div className={styles.col}>
                        <ParameterField paramKey="connect.mode" big={true} />
                    </div>
                    <div className={styles.divider}></div>
                    <div className={styles.col}>
                        <PrefabSelection paramKey={PARAM_KEYS.connect.netPrefab} />
                    </div>
                </>
            )}

            {/* Primary Controls */}
            <div className={styles.divider}></div>
            <div className={styles.row}>
                <div className={styles.actions}>
                    {selectedEntitiesBinding.length < 2 && (
                        <span className={styles.helper}>
                            {translate("NetworkTools.UI.Common.SelectAtLeastTwoNodes")}
                        </span>
                    )}
                    {selectedEntitiesBinding.length >= 2 && (
                        <Button
                            variant="primary"
                            className={styles.applyButton}
                            disabled={
                                selectedEntitiesBinding.length < 2 ||
                                activeConnectMode === ConnectMode.None
                            }
                            onSelect={() => GAME_TRIGGERS.REQUEST_APPLY()}>
                            {translate("NetworkTools.UI.Connect.ApplyCurve")}
                        </Button>
                    )}
                </div>
            </div>
        </>
    );
};
