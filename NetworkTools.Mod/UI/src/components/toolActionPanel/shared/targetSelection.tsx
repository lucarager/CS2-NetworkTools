import React from "react";
import styles from "../toolActionPanel.module.scss";
import { TargetOption } from "gameBindings";
import { useTargets } from "hooks/useTargets";
import { VC, VF, VT } from "components/vanilla/Components";
import { c } from "utils/classes";
import { useLocalization } from "cs2/l10n";

/** Human-readable metadata for each target flag. */
const TARGET_FLAGS: { flag: TargetOption; localeKey: string; icon: string }[] = [
    {
        flag: TargetOption.Road,
        localeKey: "NetworkTools.UI.Target.Road",
        icon: "coui://nt/Target/Road.svg",
    },
    {
        flag: TargetOption.Path,
        localeKey: "NetworkTools.UI.Target.Path",
        icon: "coui://nt/Target/Path.svg",
    },
    {
        flag: TargetOption.Rail,
        localeKey: "NetworkTools.UI.Target.Rail",
        icon: "coui://nt/Target/Rail.svg",
    },
    {
        flag: TargetOption.Waterway,
        localeKey: "NetworkTools.UI.Target.Waterway",
        icon: "coui://nt/Target/Waterway.svg",
    },
    {
        flag: TargetOption.InvisiblePath,
        localeKey: "NetworkTools.UI.Target.InvisiblePath",
        icon: "coui://nt/Target/InvisiblePath.svg",
    },
];

export const TargetSelection: React.FC = () => {
    const { available, selected, setSelected, hasFlag, toggleFlag } = useTargets();
    const { translate } = useLocalization();

    if (available === TargetOption.None) return null;

    const visibleFlags = TARGET_FLAGS.filter((f) => (available & f.flag) !== 0);
    const allSelected = (selected & available) === available;

    const handleToggleAll = () => {
        setSelected(allSelected ? TargetOption.None : available);
    };

    return (
        <div className={styles.controlRow}>
            <div className={styles.controlRowInner}>
                <span className={styles.paramLabel}>{translate("NetworkTools.UI.Target.Label")}</span>
                <div className={styles.buttonRow}>
                    <VC.ToolButton
                        className={c(VT.toolButton.button, styles.iconButton)}
                        src={"coui://nt/Target/All.svg"}
                        onSelect={handleToggleAll}
                        selected={allSelected}
                        multiSelect={true}
                        disabled={false}
                        focusKey={VF.FOCUS_DISABLED}
                        tooltip={translate("NetworkTools.UI.Common.ToggleAll")}
                    />
                    {visibleFlags.map((target) => (
                        <VC.ToolButton
                            key={target.flag}
                            className={c(VT.toolButton.button, styles.iconButton)}
                            src={target.icon}
                            onSelect={() => toggleFlag(target.flag)}
                            selected={hasFlag(target.flag)}
                            multiSelect={true}
                            disabled={false}
                            focusKey={VF.FOCUS_DISABLED}
                            tooltip={translate(target.localeKey)}
                        />
                    ))}
                </div>
            </div>
        </div>
    );
};
