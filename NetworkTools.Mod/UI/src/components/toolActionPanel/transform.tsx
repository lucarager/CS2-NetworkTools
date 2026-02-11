import React from "react";
import styles from "./toolActionPanel.module.scss";
import {
    GAME_BINDINGS,
    GAME_TRIGGERS,
    SlopeConfigData,
    ShapeConfigData,
    SlopeTemplate,
    ShapeTemplate,
} from "gameBindings";
import { useValue } from "cs2/api";
import { Button, Tooltip } from "cs2/ui";
import { VC, VF, VT } from "components/vanilla/Components";
import { c } from "utils/classes";

// Shape modes (XZ plane transformations)
const SHAPE_MODES: { label: string; id: ShapeTemplate; icon: string }[] = [
    { label: "Preserve", id: "preserve", icon: "coui://nt/Modes/Shape/Preserve.svg" },
    { label: "Straighten", id: "straighten", icon: "coui://nt/Modes/Shape/Straight.svg" },
    { label: "Smooth", id: "smooth", icon: "coui://nt/Modes/Shape/Smooth.svg" },
    // { label: "Equal Spacing", id: "equalspacing", icon: "coui://nt/Modes/Shape/EqualSpacing.svg" },
];

// Slope modes (Y axis transformations)
const SLOPE_MODES: { label: string; id: SlopeTemplate; icon: string }[] = [
    { label: "Preserve", id: "preserve", icon: "coui://nt/Modes/Slope/Preserve.svg" },
    { label: "Constant Slope", id: "linear", icon: "coui://nt/Modes/Slope/Linear.svg" },
    { label: "EaseInOut Slope", id: "easeinout", icon: "coui://nt/Modes/Slope/Eased.svg" },
    // { label: "Parabolic", id: "parabolic", icon: "coui://nt/Modes/Slope/Parabolic.svg" },
];

interface TransformControlProps {
    toolId?: string;
}

export const TransformControls: React.FC<TransformControlProps> = () => {
    const selectedEntitiesBinding = useValue(GAME_BINDINGS.SELECTED_ENTITIES.binding);
    const slopeConfig = useValue(GAME_BINDINGS.SLOPE_CONFIG.binding);
    const shapeConfig = useValue(GAME_BINDINGS.SHAPE_CONFIG.binding);

    const handleSlopeParameterChange = (param: keyof SlopeConfigData, value: string | number) => {
        const newConfig: SlopeConfigData = {
            ...slopeConfig,
            [param]: value,
        };
        GAME_BINDINGS.SLOPE_CONFIG.set(newConfig);
    };

    const handleShapeParameterChange = (param: keyof ShapeConfigData, value: string | number) => {
        const newConfig: ShapeConfigData = {
            ...shapeConfig,
            [param]: value,
        };
        GAME_BINDINGS.SHAPE_CONFIG.set(newConfig);
    };

    // Check if any transformation is configured
    const hasTransform = shapeConfig.template !== "preserve" || slopeConfig.template !== "preserve";

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
                    {/* Shape Controls (XZ) */}
                    <div className={styles.divider}></div>
                    <div className={styles.col}>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>Shape (XZ)</span>
                                <div className={styles.buttonRow}>
                                    {SHAPE_MODES.map((preset) => (
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
                        {shapeConfig.template === "smooth" && (
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

                    {/* Slope Controls (Y) */}
                    <div className={styles.divider}></div>
                    <div className={styles.col}>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>Slope (Y)</span>
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
                                                    slopeConfig.template === preset.id
                                                        ? styles.iconButton__active
                                                        : null,
                                                )}
                                                onSelect={() =>
                                                    handleSlopeParameterChange(
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
                        {slopeConfig.template === "easeinout" && (
                            <>
                                <div className={styles.sliderField}>
                                    <VC.FloatSliderField
                                        value={slopeConfig.easeInLength}
                                        label={"Start easing strength"}
                                        min={0}
                                        max={0.5}
                                        fractionDigits={3}
                                        onChange={(e: number) => {
                                            handleSlopeParameterChange("easeInLength", e);
                                        }}
                                    />
                                </div>
                                <div className={styles.sliderField}>
                                    <VC.FloatSliderField
                                        value={slopeConfig.easeOutLength}
                                        label={"End easing strength"}
                                        min={0}
                                        max={0.5}
                                        fractionDigits={3}
                                        onChange={(e: number) => {
                                            handleSlopeParameterChange("easeOutLength", e);
                                        }}
                                    />
                                </div>
                            </>
                        )}

                        {/* Parabolic Parameters */}
                        {slopeConfig.template === "parabolic" && (
                            <>
                                <div className={styles.controlRow}>
                                    <VC.FloatSliderField
                                        value={slopeConfig.archHeight}
                                        label={"Arch Height"}
                                        min={-1}
                                        max={1}
                                        fractionDigits={3}
                                        onChange={(e: number) => {
                                            handleSlopeParameterChange("archHeight", e);
                                        }}
                                    />
                                </div>
                                <div className={styles.controlRow}>
                                    <VC.FloatSliderField
                                        value={slopeConfig.archPosition}
                                        label={"Arch Position"}
                                        min={0.1}
                                        max={0.9}
                                        fractionDigits={3}
                                        onChange={(e: number) => {
                                            handleSlopeParameterChange("archPosition", e);
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
                            onSelect={() => GAME_TRIGGERS.APPLY_SLOPE(slopeConfig.template)}>
                            Apply Transform
                        </Button>
                    )}
                </div>
            </div>
        </>
    );
};
