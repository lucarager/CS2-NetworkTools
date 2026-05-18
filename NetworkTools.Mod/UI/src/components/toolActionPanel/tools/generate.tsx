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

    return (
        <div className={styles.section}>
            <div className={styles.section__tabs}>
                <TabBar paramKey="generate.mode" />
            </div>
            <div className={styles.section__content}>
                {activeGenerateMode === GenerateMode.Grid && (
                    <>
                        <PrefabSelection paramKey={PARAM_KEYS.generate.netPrefab} />
                        <ParameterField paramKey="generate.gridXSpacing" />
                        <ParameterField paramKey="generate.gridZSpacing" />
                        <ParameterField paramKey="generate.gridXNum" />
                        <ParameterField paramKey="generate.gridZNum" />
                        <ParameterField paramKey="generate.elevation" />
                        {/* <ParameterField paramKey="generate.altPrefabX" /> */}
                        {/* <ParameterField paramKey="generate.altPrefabZ" /> */}
                    </>
                )}
                {activeGenerateMode === GenerateMode.Circle && (
                    <ParameterField paramKey="generate.circleRadius" />
                )}
                {activeGenerateMode === GenerateMode.Oval && (
                    <>
                        <PrefabSelection paramKey={PARAM_KEYS.generate.netPrefab} />
                        <ParameterField paramKey="generate.ovalRadiusX" />
                        <ParameterField paramKey="generate.ovalRadiusZ" />
                    </>
                )}
            </div>
        </div>
    );
};
