import React from "react";
import styles from "../toolActionPanel.module.scss";
import {
    GAME_BINDINGS,
    GAME_TRIGGERS,
} from "gameBindings";
import {
    ShapeTransformTemplate,
    PARAM_BINDINGS,
    PARAM_META,
} from "generated/parameters.generated";

// Internal 0-0.5 path-ratio range is presented to the player as 0-100%.
const EASE_DISPLAY_SCALE = 200;
const easeInMeta = PARAM_META["roadShape.easeInLength"];
const easeOutMeta = PARAM_META["roadShape.easeOutLength"];
const archHeightMeta = PARAM_META["roadShape.archHeight"];
const archPositionMeta = PARAM_META["roadShape.archPosition"];
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
    const template = useValue(PARAM_BINDINGS.roadShape.template.binding);
    const easeInLength = useValue(PARAM_BINDINGS.roadShape.easeInLength.binding);
    const easeOutLength = useValue(PARAM_BINDINGS.roadShape.easeOutLength.binding);
    const archHeight = useValue(PARAM_BINDINGS.roadShape.archHeight.binding);
    const archPosition = useValue(PARAM_BINDINGS.roadShape.archPosition.binding);
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
                                </div>
                            </div>
                        </div>

                        {/* EaseInOut Parameters */}
                        {template === ShapeTransformTemplate.SlopeEaseInOut && (
                            <>
                                <div className={styles.controlRow}>
                                    <div
                                        className={c(
                                            styles.sliderField,
                                            styles.sliderField__withUnit,
                                        )}>
                                        <VC.FloatSliderField
                                            value={easeInLength * EASE_DISPLAY_SCALE}
                                            label={translate("NetworkTools.UI.Slope.StartingFlatness") ?? ""}
                                            min={easeInMeta.min * EASE_DISPLAY_SCALE}
                                            max={easeInMeta.max * EASE_DISPLAY_SCALE}
                                            fractionDigits={0}
                                            onChange={(e: number) => {
                                                PARAM_BINDINGS.roadShape.easeInLength.set(e / EASE_DISPLAY_SCALE);
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
                                        <VC.FloatSliderField
                                            value={easeOutLength * EASE_DISPLAY_SCALE}
                                            label={translate("NetworkTools.UI.Slope.EndingFlatness") ?? ""}
                                            min={easeOutMeta.min * EASE_DISPLAY_SCALE}
                                            max={easeOutMeta.max * EASE_DISPLAY_SCALE}
                                            fractionDigits={0}
                                            onChange={(e: number) => {
                                                PARAM_BINDINGS.roadShape.easeOutLength.set(e / EASE_DISPLAY_SCALE);
                                            }}
                                        />
                                        <span className={styles.unitLabel}>%</span>
                                    </div>
                                </div>
                            </>
                        )}

                        {/* Arch Parameters */}
                        {template === ShapeTransformTemplate.SlopeArch && (
                            <>
                                <div className={styles.controlRow}>
                                    <VC.FloatSliderField
                                        value={archHeight}
                                        label={translate("NetworkTools.UI.Slope.ArchHeight") ?? ""}
                                        min={archHeightMeta.min}
                                        max={archHeightMeta.max}
                                        fractionDigits={3}
                                        onChange={(e: number) => {
                                            PARAM_BINDINGS.roadShape.archHeight.set(e);
                                        }}
                                    />
                                </div>
                                <div className={styles.controlRow}>
                                    <VC.FloatSliderField
                                        value={archPosition}
                                        label={translate("NetworkTools.UI.Slope.ArchPosition") ?? ""}
                                        min={archPositionMeta.min}
                                        max={archPositionMeta.max}
                                        fractionDigits={3}
                                        onChange={(e: number) => {
                                            PARAM_BINDINGS.roadShape.archPosition.set(e);
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
