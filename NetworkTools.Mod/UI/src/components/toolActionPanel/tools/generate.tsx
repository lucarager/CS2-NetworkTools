import React from "react";
import styles from "../toolActionPanel.module.scss";
import { GAME_TRIGGERS } from "gameBindings";
import { GenerateMode, PARAM_KEYS, PARAM_BINDINGS } from "generated/parameters.generated";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { PrefabSelection } from "../shared/prefabSelection";
import { ParameterField } from "../shared/parameterField";
import { useLocalization } from "cs2/l10n";

const G = PARAM_BINDINGS.generate;

export const GenerateControls: React.FC = () => {
    const activeGenerateMode = useValue(G.mode.binding) as GenerateMode;
    const { translate } = useLocalization();

    return (
        <>
            <div className={styles.col}>
                <ParameterField paramKey="generate.mode" big={true} />
            </div>
            <div className={styles.divider}></div>
            <div className={styles.col}>
                {activeGenerateMode === GenerateMode.Grid && (
                    <>
                        <PrefabSelection paramKey={PARAM_KEYS.generate.netPrefab} />
                        <ParameterField paramKey="generate.gridXSpacing" />
                        <ParameterField paramKey="generate.gridZSpacing" />
                        <ParameterField paramKey="generate.gridXNum" />
                        <ParameterField paramKey="generate.gridZNum" />
                        <ParameterField paramKey="generate.altPrefabX" />
                        <ParameterField paramKey="generate.altPrefabZ" />
                    </>
                )}
                {activeGenerateMode === GenerateMode.Circle && (
                    <ParameterField paramKey="generate.circleRadius" />
                )}
            </div>

            {/* Apply Button */}
            <div className={styles.divider}></div>
            <div className={styles.row}>
                <div className={styles.actions}>
                    <Button
                        variant="primary"
                        className={styles.applyButton}
                        onSelect={() => GAME_TRIGGERS.REQUEST_APPLY()}>
                        {translate("NetworkTools.UI.Generate.Apply")}
                    </Button>
                </div>
            </div>
        </>
    );
};
