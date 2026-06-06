import React from "react";
import styles from "../toolActionPanel.module.scss";
import { ShapeTransformTemplate, PARAM_BINDINGS } from "generated/parameters.generated";
import { useValue } from "cs2/api";
import { TabBar } from "../shared/tabBar";
import { ParameterField } from "../shared/parameterField";

export const ShapeSlopeControls: React.FC = () => {
    const template = useValue(PARAM_BINDINGS.roadShape.template.binding);

    return (
        <div className={styles.section}>
            <div className={styles.section__tabs}>
                <TabBar paramKey="roadShape.template" group="Slope" />
            </div>
            {template === ShapeTransformTemplate.SlopeEaseInOut && (
                <div className={styles.section__content}>
                    <ParameterField paramKey="roadShape.easeInLength" />
                    <ParameterField paramKey="roadShape.easeOutLength" />
                </div>
            )}
            {template === ShapeTransformTemplate.SlopeArch && (
                <div className={styles.section__content}>
                    <ParameterField paramKey="roadShape.archHeight" />
                    <ParameterField paramKey="roadShape.archPosition" />
                </div>
            )}
        </div>
    );
};
