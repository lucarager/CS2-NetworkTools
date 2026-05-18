import React, { useEffect } from "react";
import styles from "../toolActionPanel.module.scss";
import { GAME_BINDINGS } from "gameBindings";
import { ShapeTransformTemplate, PARAM_BINDINGS } from "generated/parameters.generated";
import { useValue } from "cs2/api";
import { TabBar } from "../shared/tabBar";
import { ParameterField } from "../shared/parameterField";

export const ShapeCurveControls: React.FC<{
    toolId: number;
    onApplyDisabledChange?: (disabled: boolean) => void;
}> = ({ onApplyDisabledChange }) => {
    const selectedEntitiesBinding = useValue(GAME_BINDINGS.SELECTED_ENTITIES.binding);
    const template = useValue(PARAM_BINDINGS.roadShape.template.binding);

    // Check if any transformation is configured
    const hasTransform = template !== ShapeTransformTemplate.Preserve;

    useEffect(() => {
        onApplyDisabledChange?.(!hasTransform);
    }, [hasTransform, onApplyDisabledChange]);

    return (
        <div className={styles.section}>
            <div className={styles.section__tabs}>
                <TabBar paramKey="roadShape.template" group="Curve" />
            </div>
            <div className={styles.section__content}>
                {template === ShapeTransformTemplate.CurveSmooth && (
                    <>
                        <ParameterField paramKey="roadShape.smoothingFactor" />
                    </>
                )}
            </div>
        </div>
    );
};
