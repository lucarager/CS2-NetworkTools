import React from "react";
import styles from "../toolActionPanel.module.scss";
import { GenerateMode, PARAM_BINDINGS, PARAM_KEYS } from "generated/parameters.generated";
import { useValue } from "cs2/api";
import { PrefabSelection } from "../shared/prefabSelection";
import { ParameterField } from "../shared/parameterField";
import { TabBar } from "../shared/tabBar";

const G = PARAM_BINDINGS.generate;

export const GenerateControls: React.FC = () => {
    const activeGenerateMode = useValue(G.mode.binding) as GenerateMode;
    const altPrefabX = useValue(G.altPrefabX.binding) as boolean;
    const altPrefabZ = useValue(G.altPrefabZ.binding) as boolean;

    return (
        <div className={styles.section}>
            <div className={styles.section__tabs}>
                <TabBar paramKey="generate.mode" />
            </div>
            <div className={styles.section__content}>
                {activeGenerateMode === GenerateMode.Grid && (
                    <>
                        <PrefabSelection paramKey={PARAM_KEYS.generate.netPrefab} />
                        <ParameterField paramKey="generate.elevation" />
                        <ParameterField paramKey="generate.gridXSpacing" />
                        <ParameterField paramKey="generate.gridZSpacing" />
                        <ParameterField paramKey="generate.gridXNum" />
                        <ParameterField paramKey="generate.gridZNum" />
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
                    </>
                )}
                {activeGenerateMode === GenerateMode.Circle && (
                    <>
                        <PrefabSelection paramKey={PARAM_KEYS.generate.netPrefab} />
                        <ParameterField paramKey="generate.elevation" />
                        <ParameterField paramKey="generate.circleRadius" />
                    </>
                )}
                {activeGenerateMode === GenerateMode.Oval && (
                    <>
                        <PrefabSelection paramKey={PARAM_KEYS.generate.netPrefab} />
                        <ParameterField paramKey="generate.elevation" />
                        <ParameterField paramKey="generate.ovalRadiusX" />
                        <ParameterField paramKey="generate.ovalRadiusZ" />
                    </>
                )}
            </div>
        </div>
    );
};
