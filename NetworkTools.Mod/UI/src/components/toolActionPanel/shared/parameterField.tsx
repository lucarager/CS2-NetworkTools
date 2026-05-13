import React from "react";
import styles from "../toolActionPanel.module.scss";
import { PARAM_META, PARAM_BINDING, ENUM_OPTIONS } from "generated/parameters.generated";
import { useValue } from "cs2/api";
import { Tooltip } from "cs2/ui";
import { VC, VF, VT } from "components/vanilla/Components";
import { c } from "utils/classes";
import { useLocalization } from "cs2/l10n";

type ParamKey = keyof typeof PARAM_META;

type ParamMeta = (typeof PARAM_META)[ParamKey];

type EnumOptionsKey = keyof typeof ENUM_OPTIONS;

interface ParameterFieldProps {
    paramKey: ParamKey;
    disabled?: boolean;
    big?: boolean;
}

export const ParameterField: React.FC<ParameterFieldProps> = ({ paramKey, disabled, big }) => {
    const meta = PARAM_META[paramKey] as ParamMeta;
    const binding = PARAM_BINDING[paramKey];
    const value = useValue(binding.binding);
    const { translate } = useLocalization();
    const label = ("label" in meta ? translate(meta.label as string) : paramKey) ?? paramKey;

    switch (meta.type) {
        case "float":
            return (
                <div className={styles.controlRow}>
                    <div className={styles.vanillaField}>
                        <VC.FloatSliderField
                            value={value as number}
                            label={label}
                            min={meta.min}
                            max={meta.max}
                            fractionDigits={meta.fractionDigits}
                            disabled={disabled}
                            onChange={(v: number) => binding.set(v)}
                        />
                    </div>
                </div>
            );
        case "int":
            return (
                <div className={styles.controlRow}>
                    <div className={styles.vanillaField}>
                        <VC.IntSliderField
                            value={value as number}
                            label={label}
                            min={meta.min}
                            max={meta.max}
                            disabled={disabled}
                            onChange={(v: number) => binding.set(Math.round(v))}
                        />
                    </div>
                </div>
            );
        case "bool":
            return (
                <div className={styles.controlRow}>
                    <div className={styles.vanillaField}>
                        <VC.ToggleField
                            value={value as boolean}
                            label={label}
                            disabled={disabled}
                            onChange={(v: boolean) => binding.set(v)}
                        />
                    </div>
                </div>
            );
        case "enum": {
            const enumType = meta.enumType as EnumOptionsKey;
            const options = ENUM_OPTIONS[enumType];
            if (!options) return null;
            return (
                <div className={styles.controlRow}>
                    <div className={styles.controlRowInner}>
                        <span className={styles.paramLabel}>{label}</span>
                        <div className={styles.buttonRow}>
                            {options.map((option) => (
                                <Tooltip
                                    key={option.value}
                                    tooltip={translate(option.label)}
                                    delayTime={0}>
                                    <VC.ToolButton
                                        className={c(
                                            VT.toolButton.button,
                                            styles.iconButton,
                                            big && styles.iconButton__xl,
                                        )}
                                        src={option.icon}
                                        onSelect={() => binding.set(option.value)}
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
        default:
            return null;
    }
};
