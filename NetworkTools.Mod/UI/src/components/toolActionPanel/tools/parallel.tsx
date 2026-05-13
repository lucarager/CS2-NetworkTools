import React from "react";
import styles from "../toolActionPanel.module.scss";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import { PARAM_KEYS } from "generated/parameters.generated";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { PrefabSelection } from "../shared/prefabSelection";
import { ParameterField } from "../shared/parameterField";
import { useLocalization } from "cs2/l10n";

export const ParallelControls: React.FC = () => {
    const selectedEntities = useValue(GAME_BINDINGS.SELECTED_ENTITIES.binding);
    const { translate } = useLocalization();

    return (
        <>
            {/* Configuration Controls - Show when 2+ nodes selected */}
            {selectedEntities.length >= 2 && (
                <>
                    <div className={styles.divider}></div>
                    <div className={styles.col}>
                        <PrefabSelection paramKey={PARAM_KEYS.parallel.netPrefab} />
                        <ParameterField paramKey="parallel.reverseDirection" />
                        <ParameterField paramKey="parallel.horizontalDirection" />
                        <ParameterField paramKey="parallel.horizontalOffset" />
                        <ParameterField paramKey="parallel.verticalDirection" />
                        <ParameterField paramKey="parallel.verticalOffset" />
                    </div>
                </>
            )}

            {/* Primary Controls */}
            <div className={styles.divider}></div>
            <div className={styles.row}>
                <div className={styles.actions}>
                    {selectedEntities.length < 2 && (
                        <span className={styles.helper}>
                            {translate("NetworkTools.UI.Common.SelectAtLeastTwoNodes")}
                        </span>
                    )}
                    {selectedEntities.length >= 2 && (
                        <Button
                            variant="primary"
                            className={styles.applyButton}
                            disabled={selectedEntities.length < 2}
                            onSelect={() => GAME_TRIGGERS.REQUEST_APPLY()}>
                            {translate("NetworkTools.UI.Parallel.CreateParallel")}
                        </Button>
                    )}
                </div>
            </div>
        </>
    );
};
