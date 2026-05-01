import React from "react";
import styles from "../toolActionPanel.module.scss";
import {
    GAME_BINDINGS,
    GAME_TRIGGERS,
    ShapeConfigData,
    ShapeTransformTemplate,
} from "gameBindings";
import { useValue } from "cs2/api";
import { Button, Tooltip } from "cs2/ui";
import { VC, VF } from "components/vanilla/Components";
import { NodeSelection } from "../shared/nodeSelection";
import { c } from "utils/classes";
import { useLocalization } from "cs2/l10n";

// Slope modes (Y-axis transformations)
const SLOPE_MODES: { localeKey: string; id: ShapeTransformTemplate; icon: string }[] = [
    {
        localeKey: "NetworkTools.UI.Slope.Preserve",
        id: ShapeTransformTemplate.Preserve,
        icon: "coui://nt/Modes/Original.svg",
    },
    {
        localeKey: "NetworkTools.UI.Slope.ConstantSlope",
        id: ShapeTransformTemplate.SlopeLinear,
        icon: "coui://nt/Modes/SlopeLinear.svg",
    },
    {
        localeKey: "NetworkTools.UI.Slope.EaseInOutSlope",
        id: ShapeTransformTemplate.SlopeEaseInOut,
        icon: "coui://nt/Modes/SlopeEaseInOut.svg",
    },
];

export const ShapeSlopeControls: React.FC = () => {
    const selectedEntitiesBinding = useValue(GAME_BINDINGS.SELECTED_ENTITIES.binding);
    const shapeConfig = useValue(GAME_BINDINGS.SHAPE_CONFIG.binding);
    const { translate } = useLocalization();

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
                                <span className={styles.paramLabel}>{translate("NetworkTools.UI.Common.Mode")}</span>
                                <div className={styles.buttonRow}>
                                    {SLOPE_MODES.map((preset) => (
                                        <Tooltip
                                            key={preset.id}
                                            tooltip={translate(preset.localeKey)}
                                            delayTime={0}>
                                            <VC.ToolButton
                                                key={preset.id}
                                                className={c(
                                                    styles.iconButton,
                                                    styles.iconButton__xl,
                                                    shapeConfig.template === preset.id
                                                        ? styles.iconButton__active
                                                        : null,
                                                )}
                                                src={preset.icon}
                                                onSelect={() =>
                                                    handleShapeParameterChange(
                                                        "template",
                                                        preset.id,
                                                    )
                                                }
                                                selected={shapeConfig.template === preset.id}
                                                multiSelect={false}
                                                disabled={false}
                                                focusKey={VF.FOCUS_DISABLED}
                                            />
                                        </Tooltip>
                                    ))}
                                </div>
                            </div>
                        </div>

                        {/* EaseInOut Parameters */}
                        {shapeConfig.template === ShapeTransformTemplate.SlopeEaseInOut && (
                            <>
                                <div className={styles.controlRow}>
                                    <div
                                        className={c(
                                            styles.sliderField,
                                            styles.sliderField__withUnit,
                                        )}>
                                        {/* We mask the internal 0 - 0.5 float range into 0 - 100% for the player */}
                                        <VC.FloatSliderField
                                            value={shapeConfig.easeInLength * 200}
                                            label={translate("NetworkTools.UI.Slope.StartingFlatness") ?? ""}
                                            min={0}
                                            max={100}
                                            fractionDigits={0}
                                            onChange={(e: number) => {
                                                handleShapeParameterChange("easeInLength", e / 200);
                                            }}
                                        />
                                        <span className={styles.unitLabel}>%</span>
                                    </div>
                                </div>
                                <div className={styles.controlRow}>
                                    <div
                                        className={c(
                                            styles.sliderField,
                                            styles.sliderField__withUnit,
                                        )}>
                                        {/* We mask the internal 0 - 0.5 float range into 0 - 100% for the player */}
                                        <VC.FloatSliderField
                                            value={shapeConfig.easeOutLength * 200}
                                            label={translate("NetworkTools.UI.Slope.EndingFlatness") ?? ""}
                                            min={0}
                                            max={100}
                                            fractionDigits={0}
                                            onChange={(e: number) => {
                                                handleShapeParameterChange(
                                                    "easeOutLength",
                                                    e / 200,
                                                );
                                            }}
                                        />
                                        <span className={styles.unitLabel}>%</span>
                                    </div>
                                </div>
                            </>
                        )}

                        {/* Parabolic Parameters */}
                        {shapeConfig.template === ShapeTransformTemplate.SlopeParabolic && (
                            <>
                                <div className={styles.controlRow}>
                                    <VC.FloatSliderField
                                        value={shapeConfig.archHeight}
                                        label={translate("NetworkTools.UI.Slope.ArchHeight") ?? ""}
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
                                        label={translate("NetworkTools.UI.Slope.ArchPosition") ?? ""}
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
                        <span className={styles.helper}>{translate("NetworkTools.UI.Common.SelectAtLeastTwoNodes")}</span>
                    )}
                    {selectedEntitiesBinding.length >= 2 && (
                        <Button
                            variant="primary"
                            className={styles.applyButton}
                            disabled={selectedEntitiesBinding.length < 2 || !hasTransform}
                            onSelect={() => GAME_TRIGGERS.REQUEST_APPLY()}>
                            {translate("NetworkTools.UI.Slope.ApplySlope")}
                        </Button>
                    )}
                </div>
            </div>
        </>
    );
};
