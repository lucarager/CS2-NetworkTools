import React from "react";
import styles from "../toolActionPanel.module.scss";
import {
    PARAM_META,
    PARAM_BINDING,
    ENUM_OPTIONS,
    ShapeTransformTemplate,
} from "generated/parameters.generated";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";

type ParamKey = keyof typeof PARAM_META;
type EnumOptionsKey = keyof typeof ENUM_OPTIONS;

interface TabBarProps {
    paramKey: ParamKey;
    group?: string;
}

export const TabBar: React.FC<TabBarProps> = ({ paramKey, group }) => {
    const binding = PARAM_BINDING[paramKey];
    const value = useValue(binding.binding);
    const { translate } = useLocalization();

    const meta = PARAM_META[paramKey];
    if (meta.type !== "enum") return null;
    const enumType = (meta as { enumType: string }).enumType;
    const optionsKey = (group ? `${enumType}.${group}` : enumType) as EnumOptionsKey;
    const options = ENUM_OPTIONS[optionsKey];
    if (!options) return null;

    const visibleOptions = options.filter((o) => o.visible !== false);

    return (
        <div className={styles.tabBar}>
            {visibleOptions.map((option) => (
                <button
                    key={option.value}
                    className={`${styles.tab} ${value === option.value ? styles.tab__active : ""} ${option.disabled ? styles.tab__disabled : ""}`}
                    style={{ width: `${100 / visibleOptions.length}%` }}
                    onClick={() => binding.set(option.value)}
                    disabled={option.disabled}>
                    <img src={option.icon} className={styles.tab__icon} />
                    <span className={styles.tab__label}>{translate(option.label)}</span>
                    {option.value == ShapeTransformTemplate.CurveSmooth && (
                        <span className={styles.tab__label}>
                            ({translate("NetworkTools.UI.Common.ComingSoon")})
                        </span>
                    )}
                </button>
            ))}
        </div>
    );
};
