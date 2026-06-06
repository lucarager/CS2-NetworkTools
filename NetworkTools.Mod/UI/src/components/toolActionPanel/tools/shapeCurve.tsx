import React from "react";
import styles from "../toolActionPanel.module.scss";
import { ShapeTransformTemplate, PARAM_BINDINGS } from "generated/parameters.generated";
import { useValue } from "cs2/api";
import { TabBar } from "../shared/tabBar";
import { ParameterField } from "../shared/parameterField";

export const ShapeCurveControls: React.FC = () => {
    const template = useValue(PARAM_BINDINGS.roadShape.template.binding);

    return (
        <div className={styles.section}>
            <div className={styles.section__tabs}>
                <TabBar paramKey="roadShape.template" group="Curve" />
            </div>
            {template === ShapeTransformTemplate.CurveSmooth && (
                <div className={styles.section__content}>
                    <ParameterField paramKey="roadShape.smoothingFactor" />
                </div>
            )}
        </div>
    );
};
