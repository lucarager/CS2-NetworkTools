import React from "react";
import styles from "../toolActionPanel.module.scss";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import { ShapeTransformTemplate, PARAM_BINDINGS, PARAM_META } from "generated/parameters.generated";

const smoothingFactorMeta = PARAM_META["roadShape.smoothingFactor"];
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
    const template = useValue(PARAM_BINDINGS.roadShape.template.binding);
    const smoothingFactor = useValue(PARAM_BINDINGS.roadShape.smoothingFactor.binding);
    const { translate } = useLocalization();

    // Check if any transformation is configured
    const hasTransform = template !== ShapeTransformTemplate.Preserve;

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
                                <span className={styles.paramLabel}>
                                    {translate("NetworkTools.UI.Common.Mode")}
                                </span>
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
                                                    template === preset.id
                                                        ? styles.iconButton__active
                                                        : null,
                                                )}
                                                src={preset.icon}
                                                onSelect={() =>
                                                    PARAM_BINDINGS.roadShape.template.set(preset.id)
                                                }
                                                selected={template === preset.id}
                                                multiSelect={false}
                                                disabled={false}
                                                focusKey={VF.FOCUS_DISABLED}
                                            />
                                        </Tooltip>
                                    ))}
                                    <Tooltip
                                        tooltip={`${translate("NetworkTools.UI.Curve.SmoothCurve", "Smooth Curve")} (${translate("NetworkTools.UI.Common.ComingSoon")})`}
                                        delayTime={0}>
                                        <VC.ToolButton
                                            className={c(styles.iconButton, styles.iconButton__xl)}
                                            src={"coui://nt/Modes/CurveSmooth.svg"}
                                            multiSelect={false}
                                            disabled={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                        />
                                    </Tooltip>
                                </div>
                            </div>
                        </div>

                        {/* Smooth Parameters */}
                        {template === ShapeTransformTemplate.CurveSmooth && (
                            <div className={styles.sliderField}>
                                <VC.FloatSliderField
                                    value={smoothingFactor}
                                    label={translate("NetworkTools.UI.Curve.SmoothingFactor") ?? ""}
                                    min={smoothingFactorMeta.min}
                                    max={smoothingFactorMeta.max}
                                    fractionDigits={2}
                                    onChange={(e: number) => {
                                        PARAM_BINDINGS.roadShape.smoothingFactor.set(e);
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
                        <span className={styles.helper}>
                            {translate("NetworkTools.UI.Common.SelectAtLeastTwoNodes")}
                        </span>
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
