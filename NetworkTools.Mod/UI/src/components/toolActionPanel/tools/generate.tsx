import React, { useEffect } from "react";
import styles from "../toolActionPanel.module.scss";
import { GenerateMode, PARAM_BINDINGS, PARAM_KEYS } from "generated/parameters.generated";
import { useValue } from "cs2/api";
import { GAME_BINDINGS, OperationPhase } from "gameBindings";
import { PrefabSelection } from "../shared/prefabSelection";
import { ParameterField } from "../shared/parameterField";
import { TabBar } from "../shared/tabBar";
import { useLocalization } from "cs2/l10n";

const G = PARAM_BINDINGS.generate;

export const GenerateControls: React.FC<{
    toolId: number;
    onApplyDisabledChange?: (disabled: boolean) => void;
}> = ({ onApplyDisabledChange }) => {
    const activeGenerateMode = useValue(G.mode.binding) as GenerateMode;
    const altPrefabX = useValue(G.altPrefabX.binding) as boolean;
    const altPrefabZ = useValue(G.altPrefabZ.binding) as boolean;
    const operationPhase = useValue(GAME_BINDINGS.OPERATION_PHASE.binding) as OperationPhase;
    const { translate } = useLocalization();

    useEffect(() => {
        onApplyDisabledChange?.(operationPhase !== OperationPhase.Ready);
    }, [operationPhase, onApplyDisabledChange]);

    return (
        <div className={styles.section}>
            <div className={styles.section__tabs}>
                <TabBar paramKey="generate.mode" />
            </div>
            {activeGenerateMode === GenerateMode.Grid && (
                <div className={styles.section__content}>
                    <PrefabSelection paramKey={PARAM_KEYS.generate.netPrefab} />
                    <ParameterField paramKey="generate.gridXNum" />
                    <ParameterField paramKey="generate.gridZNum" />
                    <ParameterField paramKey="generate.gridXSpacing" />
                    <ParameterField paramKey="generate.gridZSpacing" />
                    <ParameterField paramKey="generate.elevation" />
                    <ParameterField paramKey="generate.followTerrain" />
                    <div className={styles.sectionDivider}>
                        <div className={styles.sectionDivider__line}></div>
                        <span className={styles.sectionDivider__label}>
                            {translate("NetworkTools.UI.Common.Advanced")}
                        </span>
                        <div className={styles.sectionDivider__line}></div>
                    </div>
                    <ParameterField paramKey="generate.altPrefabX" />
                    {altPrefabX && (
                        <>
                            <PrefabSelection paramKey={PARAM_KEYS.generate.altNetPrefabX} />
                            <ParameterField paramKey="generate.altEveryX" />
                        </>
                    )}
                    <ParameterField paramKey="generate.altPrefabZ" />
                    {altPrefabZ && (
                        <>
                            <PrefabSelection paramKey={PARAM_KEYS.generate.altNetPrefabZ} />
                            <ParameterField paramKey="generate.altEveryZ" />
                        </>
                    )}
                </div>
            )}
            {activeGenerateMode === GenerateMode.Circle && (
                <div className={styles.section__content}>
                    <PrefabSelection paramKey={PARAM_KEYS.generate.netPrefab} />
                    <ParameterField paramKey="generate.circleRadius" />
                    <ParameterField paramKey="generate.elevation" />
                    <ParameterField paramKey="generate.followTerrain" />
                </div>
            )}
            {activeGenerateMode === GenerateMode.Oval && (
                <div className={styles.section__content}>
                    <PrefabSelection paramKey={PARAM_KEYS.generate.netPrefab} />
                    <ParameterField paramKey="generate.ovalRadiusX" />
                    <ParameterField paramKey="generate.ovalRadiusZ" />
                    <ParameterField paramKey="generate.elevation" />
                    <ParameterField paramKey="generate.followTerrain" />
                </div>
            )}
        </div>
    );
};
