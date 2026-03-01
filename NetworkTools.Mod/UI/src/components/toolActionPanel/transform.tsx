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
import { VC, VF, VT } from "components/vanilla/Components";
import { c } from "utils/classes";

// Shape modes (XZ plane transformations)
const MODES: { label: string; tag?: string; id: ShapeTransformTemplate; icon: string }[] = [
    {
        label: "Preserve",
        id: ShapeTransformTemplate.Preserve,
        icon: "coui://nt/Modes/Shape/Preserve.svg",
    },
    {
        label: "Straighten Curve",
        tag: "curve",
        id: ShapeTransformTemplate.CurveStraighten,
        icon: "coui://nt/Modes/Shape/Straight.svg",
    },
    {
        label: "Smooth Curve",
        tag: "curve",
        id: ShapeTransformTemplate.CurveSmooth,
        icon: "coui://nt/Modes/Shape/Smooth.svg",
    },
    {
        label: "Constant Slope",
        tag: "slope",
        id: ShapeTransformTemplate.SlopeLinear,
        icon: "coui://nt/Modes/Slope/Linear.svg",
    },
    {
        label: "EaseInOut Slope",
        tag: "slope",
        id: ShapeTransformTemplate.SlopeEaseInOut,
        icon: "coui://nt/Modes/Slope/Eased.svg",
    },
];

interface TransformControlProps {
    toolId?: string;
}

export const ShapeSlopeControls: React.FC<TransformControlProps> = () => {
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

    console.log(shapeConfig);

    return (
        <>
            {/* Node Selection */}
            <div className={styles.divider}></div>
            <div className={styles.col}>
                {selectedEntitiesBinding.length == 0 && (
                    <span className={styles.helper}>No nodes selected.</span>
                )}
                {selectedEntitiesBinding.length > 0 && (
                    <div>
                        {selectedEntitiesBinding.map((s, i) => (
                            <div key={i} className={styles.selectedEntity}>
                                {s.Name}
                                <VC.ToolButton
                                    src={"Media/Game/Icons/MapMarker.svg"}
                                    onSelect={() => VC.focusEntity(s.Entity)}
                                    multiSelect={false}
                                    className={VT.toolButton.button}
                                    focusKey={VF.FOCUS_DISABLED}
                                    tooltip={"Focus on Entity"}
                                />
                            </div>
                        ))}
                    </div>
                )}
            </div>

            {/* Transform Controls - Show when 2+ nodes selected */}
            {selectedEntitiesBinding.length >= 2 && (
                <>
                    <div className={styles.divider}></div>
                    <div className={styles.col}>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>Mode</span>
                                <div className={styles.buttonRow}>
                                    {MODES.map((preset) => (
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
                                                {preset.tag && (
                                                    <div className={styles.tag}>{preset.tag}</div>
                                                )}
                                                <img src={preset.icon} className={styles.icon} />
                                            </Button>
                                        </Tooltip>
                                    ))}
                                </div>
                            </div>
                        </div>

                        {/* Smooth Parameters */}
                        {/* {shapeConfig.template === ShapeTransformTemplate.CurveSmooth && (
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
                        )} */}

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
                            Apply Transform
                        </Button>
                    )}
                </div>
            </div>
        </>
    );
};
