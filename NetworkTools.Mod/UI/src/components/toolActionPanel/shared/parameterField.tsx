/**
 * Renders a UI control for a single tool parameter, chosen automatically
 * from the parameter's type and numberType.
 *
 * Dispatch works in two tiers:
 *   1. Try "{type}:{numberType}" (e.g. "int:rows") for a specialised renderer.
 *   2. Fall back to "{type}" (e.g. "int") for the default renderer.
 *
 * To add a new variant, define a component that accepts FieldRenderProps
 * and register it in FIELD_RENDERERS below.
 */

import React from "react";
import styles from "../toolActionPanel.module.scss";
import {
    PARAM_META,
    PARAM_BINDING,
    ENUM_OPTIONS,
    NumberType,
} from "generated/parameters.generated";
import { useValue } from "cs2/api";
import { Tooltip } from "cs2/ui";
import { VC, VF, VT } from "components/vanilla/Components";
import { c } from "utils/classes";
import { useLocalization } from "cs2/l10n";

// ─── Types ───────────────────────────────────────────────────────────

type ParamKey = keyof typeof PARAM_META;
type ParamMeta = (typeof PARAM_META)[ParamKey];
type FloatMeta = Extract<ParamMeta, { type: "float" }>;
type IntMeta = Extract<ParamMeta, { type: "int" }>;
type EnumMeta = Extract<ParamMeta, { type: "enum" }>;
type EnumOptionsKey = keyof typeof ENUM_OPTIONS;

export interface FieldRenderProps {
    meta: ParamMeta;
    value: unknown;
    label: string;
    disabled?: boolean;
    big?: boolean;
    onChange: (v: any) => void;
    translate: (key: string) => string | null;
}

// ─── Constants ───────────────────────────────────────────────────────

const UNIT_LABELS: Partial<Record<NumberType, string>> = {
    distance: "m",
    percentage: "%",
};

// ─── Renderer map & resolution ───────────────────────────────────────

const FIELD_RENDERERS: Record<string, React.FC<FieldRenderProps>> = {
    float: FloatSlider,
    int: IntSlider,
    "int:rows": IntStepper,
    "int:columns": IntStepper,
    bool: BoolToggle,
    enum: EnumButtons,
};

/** Looks up the renderer for a given meta — tries "type:numberType" first, then "type". */
function resolveRenderer(meta: ParamMeta): React.FC<FieldRenderProps> | undefined {
    if ("numberType" in meta && meta.numberType !== "none") {
        const specific = FIELD_RENDERERS[`${meta.type}:${meta.numberType}`];
        if (specific) return specific;
    }
    return FIELD_RENDERERS[meta.type];
}

// ─── Public component ────────────────────────────────────────────────

interface ParameterFieldProps {
    paramKey: ParamKey;
    disabled?: boolean;
    big?: boolean;
}

/** Resolves and renders the appropriate control for a parameter key from PARAM_META. */
export const ParameterField: React.FC<ParameterFieldProps> = ({ paramKey, disabled, big }) => {
    const meta = PARAM_META[paramKey] as ParamMeta;
    const binding = PARAM_BINDING[paramKey];
    const value = useValue(binding.binding);
    const { translate } = useLocalization();
    const label = ("label" in meta ? translate(meta.label as string) : paramKey) ?? paramKey;

    const Renderer = resolveRenderer(meta);
    if (!Renderer) return null;
    return (
        <Renderer
            meta={meta}
            value={value}
            label={label}
            disabled={disabled}
            big={big}
            onChange={(value) => binding.set(value)}
            translate={translate}
        />
    );
};

// ─── Renderers ───────────────────────────────────────────────────────

/** Continuous slider for float parameters. Shows a zero-mark when the range spans zero. */
function FloatSlider({ meta, value, label, disabled, onChange }: FieldRenderProps) {
    const m = meta as FloatMeta;
    const unitLabel = UNIT_LABELS[m.numberType] ?? null;
    const showZeroMark = m.min < 0 && m.max > 0;
    return (
        <div className={styles.controlRow}>
            <div className={styles.vanillaField}>
                <VC.FloatSliderField
                    value={(value as number) * m.displayScale}
                    label={label}
                    min={m.min * m.displayScale}
                    max={m.max * m.displayScale}
                    fractionDigits={m.fractionDigits}
                    disabled={disabled}
                    onChange={(v: number) => onChange(v / m.displayScale)}
                />
                {showZeroMark && (
                    <div className={styles.zeroMark}>
                        <div className={styles.zeroMark__tick} />
                    </div>
                )}
                {unitLabel && <span className={styles.unitLabel}>{unitLabel}</span>}
            </div>
        </div>
    );
}

/** Continuous slider for integer parameters. Values are rounded on change. */
function IntSlider({ meta, value, label, disabled, onChange }: FieldRenderProps) {
    const m = meta as IntMeta;
    const unitLabel = UNIT_LABELS[m.numberType] ?? null;
    return (
        <div className={styles.controlRow}>
            <div className={styles.vanillaField}>
                <VC.IntSliderField
                    value={(value as number) * m.displayScale}
                    label={label}
                    min={m.min * m.displayScale}
                    max={m.max * m.displayScale}
                    disabled={disabled}
                    onChange={(v: number) => onChange(Math.round(v / m.displayScale))}
                />
                {unitLabel && <span className={styles.unitLabel}>{unitLabel}</span>}
            </div>
        </div>
    );
}

/** Decrement/increment buttons flanking a numeric display. Used for discrete counts (rows, columns). */
function IntStepper({ meta, value, label, disabled, onChange, translate }: FieldRenderProps) {
    const m = meta as IntMeta;
    const current = (value as number) * m.displayScale;
    const min = m.min * m.displayScale;
    const max = m.max * m.displayScale;
    const unitLabel = UNIT_LABELS[m.numberType] ?? null;
    const atMin = current <= min;
    const atMax = current >= max;

    return (
        <div className={styles.controlRow}>
            <div className={styles.vanillaField}>
                <VC.Section focusKey={VF.FOCUS_DISABLED} title={label}>
                    <VC.ToolButton
                        onSelect={() => {
                            if (!atMin) onChange(Math.round((current - 1) / m.displayScale));
                        }}
                        src="Media/Glyphs/ThickStrokeArrowDown.svg"
                        focusKey={VF.FOCUS_DISABLED}
                        disabled={disabled || atMin}
                        tooltip={translate("NetworkTools.UI.Common.Decrease")}
                        className={c(
                            VT.toolButton.button,
                            styles.iconButton,
                            VT.mouseToolOptions.startButton,
                        )}
                    />
                    <div className={c(VT.mouseToolOptions.numberField)}>
                        {current}
                        {unitLabel}
                    </div>
                    <VC.ToolButton
                        onSelect={() => {
                            if (!atMax) onChange(Math.round((current + 1) / m.displayScale));
                        }}
                        src="Media/Glyphs/ThickStrokeArrowUp.svg"
                        focusKey={VF.FOCUS_DISABLED}
                        disabled={disabled || atMax}
                        tooltip={translate("NetworkTools.UI.Common.Increase")}
                        className={c(
                            VT.toolButton.button,
                            styles.iconButton,
                            VT.mouseToolOptions.endButton,
                        )}
                    />
                </VC.Section>
            </div>
        </div>
    );
}

/** Simple on/off toggle for boolean parameters. */
function BoolToggle({ value, label, disabled, onChange }: FieldRenderProps) {
    return (
        <div className={styles.controlRow}>
            <div className={styles.vanillaField}>
                <VC.ToggleField
                    value={value as boolean}
                    label={label}
                    disabled={disabled}
                    onChange={(v: boolean) => onChange(v)}
                />
            </div>
        </div>
    );
}

/** Row of icon buttons for enum parameters. Each option is a selectable icon with a tooltip. */
function EnumButtons({ meta, value, label, disabled, big, onChange, translate }: FieldRenderProps) {
    const m = meta as EnumMeta;
    const options = ENUM_OPTIONS[m.enumType as EnumOptionsKey];
    if (!options) return null;
    return (
        <div className={styles.controlRow}>
            <div className={styles.controlRowInner}>
                <span className={styles.paramLabel}>{label}</span>
                <div className={styles.buttonRow}>
                    {options.map((option) => (
                        <Tooltip key={option.value} tooltip={translate(option.label)} delayTime={0}>
                            <VC.ToolButton
                                className={c(
                                    VT.toolButton.button,
                                    styles.iconButton,
                                    big && styles.iconButton__xl,
                                )}
                                src={option.icon}
                                onSelect={() => onChange(option.value)}
                                selected={value === option.value}
                                multiSelect={false}
                                disabled={disabled ?? false}
                                focusKey={VF.FOCUS_DISABLED}
                            />
                        </Tooltip>
                    ))}
                </div>
            </div>
        </div>
    );
}
