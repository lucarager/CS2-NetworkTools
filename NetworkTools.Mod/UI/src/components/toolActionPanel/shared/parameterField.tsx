import React from "react";
import styles from "../toolActionPanel.module.scss";
import { PARAM_META, PARAM_BINDING } from "generated/parameters.generated";
import { useValue } from "cs2/api";
import { VC } from "components/vanilla/Components";
import { useLocalization } from "cs2/l10n";

type ParamKey = keyof typeof PARAM_META;

type ParamMeta = (typeof PARAM_META)[ParamKey];

interface ParameterFieldProps {
    paramKey: ParamKey;
    disabled?: boolean;
}

export const ParameterField: React.FC<ParameterFieldProps> = ({ paramKey, disabled }) => {
    const meta = PARAM_META[paramKey] as ParamMeta;
    const binding = PARAM_BINDING[paramKey];
    const value = useValue(binding.binding);
    const { translate } = useLocalization();
    const label = ("label" in meta ? translate(meta.label as string) : paramKey) ?? paramKey;

    switch (meta.type) {
        case "float":
            return (
                <div className={styles.controlRow}>
                    <div className={styles.sliderField}>
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
                    <div className={styles.sliderField}>
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
                    <VC.ToggleField
                        value={value as boolean}
                        label={label}
                        disabled={disabled}
                        onChange={(v: boolean) => binding.set(v)}
                    />
                </div>
            );
        default:
            return null;
    }
};
