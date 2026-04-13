import React from "react";
import styles from "../toolActionPanel.module.scss";
import { TargetOption } from "gameBindings";
import { useTargets } from "hooks/useTargets";
import { VC, VF, VT } from "components/vanilla/Components";
import { c } from "utils/classes";

/** Human-readable metadata for each target flag. */
const TARGET_FLAGS: { flag: TargetOption; label: string; icon: string }[] = [
    {
        flag: TargetOption.Road,
        label: "Road",
        icon: "coui://nt/Target/Road.svg",
    },
    {
        flag: TargetOption.Path,
        label: "Path",
        icon: "coui://nt/Target/Path.svg",
    },
    {
        flag: TargetOption.Rail,
        label: "Rail",
        icon: "coui://nt/Target/Rail.svg",
    },
    {
        flag: TargetOption.Waterway,
        label: "Waterway",
        icon: "coui://nt/Target/Waterway.svg",
    },
    {
        flag: TargetOption.InvisiblePath,
        label: "InvisiblePath",
        icon: "coui://nt/Target/InvisiblePath.svg",
    },
];

export const TargetSelection: React.FC = () => {
    const { available, selected, setSelected, hasFlag, toggleFlag } = useTargets();

    if (available === TargetOption.None) return null;

    const visibleFlags = TARGET_FLAGS.filter((f) => (available & f.flag) !== 0);
    const allSelected = (selected & available) === available;

    const handleToggleAll = () => {
        setSelected(allSelected ? TargetOption.None : available);
    };

    return (
        <div className={styles.controlRow}>
            <div className={styles.controlRowInner}>
                <span className={styles.paramLabel}>Targets</span>
                <div className={styles.buttonRow}>
                    <VC.ToolButton
                        className={c(VT.toolButton.button, styles.iconButton)}
                        src={"coui://nt/Target/All.svg"}
                        onSelect={handleToggleAll}
                        selected={allSelected}
                        multiSelect={true}
                        disabled={false}
                        focusKey={VF.FOCUS_DISABLED}
                        tooltip={"Toggle All"}
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
                            tooltip={target.label}
                        />
                    ))}
                </div>
            </div>
        </div>
    );
};
