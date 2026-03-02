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

// Slope modes (Y-axis transformations)
const SLOPE_MODES: { label: string; id: ShapeTransformTemplate; icon: string }[] = [
    {
        label: "Preserve",
        id: ShapeTransformTemplate.Preserve,
        icon: "coui://nt/Modes/Original.svg",
    },
    {
        label: "Constant Slope",
        id: ShapeTransformTemplate.SlopeLinear,
        icon: "coui://nt/Modes/SlopeLinear.svg",
    },
    {
        label: "EaseInOut Slope",
        id: ShapeTransformTemplate.SlopeEaseInOut,
        icon: "coui://nt/Modes/SlopeEaseInOut.svg",
    },
];

export const ShapeSlopeControls: React.FC = () => {
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
                    <div className={styles.divider}></div>
                    <div className={styles.col}>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>Mode</span>
                                <div className={styles.buttonRow}>
                                    {SLOPE_MODES.map((preset) => (
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

                        {/* EaseInOut Parameters */}
                        {shapeConfig.template === ShapeTransformTemplate.SlopeEaseInOut && (
                            <>
                                <div className={styles.sliderField}>
                                    <VC.FloatSliderField
                                        value={shapeConfig.easeInLength}
                                        label={"Start easing strength"}
                                        min={0}
                                        max={0.5}
                                        fractionDigits={3}
                                        onChange={(e: number) => {
                                            handleShapeParameterChange("easeInLength", e);
                                        }}
                                    />
                                </div>
                                <div className={styles.sliderField}>
                                    <VC.FloatSliderField
                                        value={shapeConfig.easeOutLength}
                                        label={"End easing strength"}
                                        min={0}
                                        max={0.5}
                                        fractionDigits={3}
                                        onChange={(e: number) => {
                                            handleShapeParameterChange("easeOutLength", e);
                                        }}
                                    />
                                </div>
                            </>
                        )}

                        {/* Parabolic Parameters */}
                        {shapeConfig.template === ShapeTransformTemplate.SlopeParabolic && (
                            <>
                                <div className={styles.controlRow}>
                                    <VC.FloatSliderField
                                        value={shapeConfig.archHeight}
                                        label={"Arch Height"}
                                        min={-1}
                                        max={1}
                                        fractionDigits={3}
                                        onChange={(e: number) => {
                                            handleShapeParameterChange("archHeight", e);
                                        }}
                                    />
                                </div>
                                <div className={styles.controlRow}>
                                    <VC.FloatSliderField
                                        value={shapeConfig.archPosition}
                                        label={"Arch Position"}
                                        min={0.1}
                                        max={0.9}
                                        fractionDigits={3}
                                        onChange={(e: number) => {
                                            handleShapeParameterChange("archPosition", e);
                                        }}
                                    />
                                </div>
                            </>
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
                            onSelect={() => GAME_TRIGGERS.APPLY_SLOPE()}>
                            Apply Slope
                        </Button>
                    )}
                </div>
            </div>
        </>
    );
};
