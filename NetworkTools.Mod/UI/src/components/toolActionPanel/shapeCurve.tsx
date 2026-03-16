import React from "react";
import styles from "./toolActionPanel.module.scss";
import {
    GAME_BINDINGS,
    GAME_TRIGGERS,
    ShapeConfigData,
    ShapeTransformTemplate,
} from "gameBindings";
import { useValue } from "cs2/api";
import { Button, Tooltip } from "cs2/ui";
import { VC } from "components/vanilla/Components";
import { NodeSelection } from "./nodeSelection";
import { c } from "utils/classes";

// Curve modes (XZ plane transformations)
const CURVE_MODES: { label: string; id: ShapeTransformTemplate; icon: string }[] = [
    {
        label: "Preserve",
        id: ShapeTransformTemplate.Preserve,
        icon: "coui://nt/Modes/Original.svg",
    },
    {
        label: "Straighten Curve",
        id: ShapeTransformTemplate.CurveStraighten,
        icon: "coui://nt/Modes/CurveStraighten.svg",
    },
    // {
    //     label: "Smooth Curve",
    //     id: ShapeTransformTemplate.CurveSmooth,
    //     icon: "coui://nt/Modes/CurveSmooth.svg",
    // },
];

export const ShapeCurveControls: React.FC = () => {
    const selectedEntitiesBinding = useValue(GAME_BINDINGS.SELECTED_ENTITIES.binding);
    const shapeConfig = useValue(GAME_BINDINGS.SHAPE_CONFIG.binding);

    const handleShapeParameterChange = (param: keyof ShapeConfigData, value: string | number) => {
        const newConfig: ShapeConfigData = {
            ...shapeConfig,
            [param]: value,
        };
        GAME_BINDINGS.SHAPE_CONFIG.set(newConfig);
    };

    // Check if any transformation is configured
    const hasTransform = shapeConfig.template !== ShapeTransformTemplate.Preserve;

    return (
        <>
            <NodeSelection selectedEntities={selectedEntitiesBinding} />

            {/* Transform Controls - Show when 2+ nodes selected */}
            {selectedEntitiesBinding.length >= 2 && (
                <>
                    <div className={styles.col}>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>Mode</span>
                                <div className={styles.buttonRow}>
                                    {CURVE_MODES.map((preset) => (
                                        <Tooltip
                                            key={preset.id}
                                            tooltip={preset.label}
                                            delayTime={0}>
                                            <Button
                                                key={preset.id}
                                                variant="primary"
                                                className={c(
                                                    styles.iconButton,
                                                    shapeConfig.template === preset.id
                                                        ? styles.iconButton__active
                                                        : null,
                                                )}
                                                onSelect={() =>
                                                    handleShapeParameterChange(
                                                        "template",
                                                        preset.id,
                                                    )
                                                }>
                                                <img src={preset.icon} className={styles.icon} />
                                            </Button>
                                        </Tooltip>
                                    ))}
                                </div>
                            </div>
                        </div>

                        {/* Smooth Parameters */}
                        {shapeConfig.template === ShapeTransformTemplate.CurveSmooth && (
                            <div className={styles.sliderField}>
                                <VC.FloatSliderField
                                    value={shapeConfig.smoothingFactor}
                                    label={"Smoothing Factor"}
                                    min={0}
                                    max={1}
                                    fractionDigits={2}
                                    onChange={(e: number) => {
                                        handleShapeParameterChange("smoothingFactor", e);
                                    }}
                                />
                            </div>
                        )}
                    </div>
                </>
            )}

            {/* Primary Controls */}
            <div className={styles.divider}></div>
            <div className={styles.row}>
                <div className={styles.actions}>
                    {selectedEntitiesBinding.length < 2 && (
                        <span className={styles.helper}>Select at least two nodes.</span>
                    )}
                    {selectedEntitiesBinding.length >= 2 && (
                        <Button
                            variant="primary"
                            className={styles.applyButton}
                            disabled={selectedEntitiesBinding.length < 2 || !hasTransform}
                            onSelect={() => GAME_TRIGGERS.APPLY_TRANSFORM()}>
                            Apply Curve
                        </Button>
                    )}
                </div>
            </div>
        </>
    );
};
