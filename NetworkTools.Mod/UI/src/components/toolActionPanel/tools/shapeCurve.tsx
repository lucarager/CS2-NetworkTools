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

// Curve modes (XZ plane transformations)
const CURVE_MODES: { localeKey: string; id: ShapeTransformTemplate; icon: string }[] = [
    {
        localeKey: "NetworkTools.UI.Curve.Preserve",
        id: ShapeTransformTemplate.Preserve,
        icon: "coui://nt/Modes/Original.svg",
    },
    {
        localeKey: "NetworkTools.UI.Curve.StraightenCurve",
        id: ShapeTransformTemplate.CurveStraighten,
        icon: "coui://nt/Modes/CurveStraighten.svg",
    },
    // {
    //     localeKey: "NetworkTools.UI.Curve.SmoothCurve",
    //     id: ShapeTransformTemplate.CurveSmooth,
    //     icon: "coui://nt/Modes/CurveSmooth.svg",
    // },
];

export const ShapeCurveControls: React.FC = () => {
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
                                    {CURVE_MODES.map((preset) => (
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
                                            {/* <Button
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
                                            </Button> */}
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
                                    label={translate("NetworkTools.UI.Curve.SmoothingFactor") ?? ""}
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
                        <span className={styles.helper}>{translate("NetworkTools.UI.Common.SelectAtLeastTwoNodes")}</span>
                    )}
                    {selectedEntitiesBinding.length >= 2 && (
                        <Button
                            variant="primary"
                            className={styles.applyButton}
                            disabled={selectedEntitiesBinding.length < 2 || !hasTransform}
                            onSelect={() => GAME_TRIGGERS.REQUEST_APPLY()}>
                            {translate("NetworkTools.UI.Curve.ApplyCurve")}
                        </Button>
                    )}
                </div>
            </div>
        </>
    );
};
