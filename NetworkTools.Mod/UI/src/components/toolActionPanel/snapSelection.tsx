import React from "react";
import styles from "./toolActionPanel.module.scss";
import { SnapOption } from "gameBindings";
import { useSnap } from "hooks/useSnap";
import { VC, VF, VT } from "components/vanilla/Components";
import { c } from "utils/classes";

/** Human-readable metadata for each snap flag. */
const SNAP_FLAGS: { flag: SnapOption; label: string; icon: string }[] = [
    {
        flag: SnapOption.ZoneGrid,
        label: "Zone Grid",
        icon: "coui://nt/Snap/ZoneGrid.svg",
    },
    {
        flag: SnapOption.MidPoint,
        label: "Mid Point",
        icon: "coui://nt/Snap/MidPoint.svg",
    },
];

export const SnapSelection: React.FC = () => {
    const { available, selected, setSelected, hasFlag, toggleFlag } = useSnap();

    if (available === SnapOption.None) return null;

    const visibleFlags = SNAP_FLAGS.filter((f) => (available & f.flag) !== 0);
    const allSelected = (selected & available) === available;

    const handleToggleAll = () => {
        setSelected(allSelected ? SnapOption.None : available);
    };

    return (
        <div className={styles.controlRow}>
            <div className={styles.controlRowInner}>
                <span className={styles.paramLabel}>Snapping</span>
                <div className={styles.buttonRow}>
                    <VC.ToolButton
                        className={c(VT.toolButton.button, styles.toolButton)}
                        src={"coui://nt/Snap/All.svg"}
                        onSelect={handleToggleAll}
                        selected={allSelected}
                        multiSelect={true}
                        disabled={false}
                        focusKey={VF.FOCUS_DISABLED}
                        tooltip={"Toggle All"}
                    />
                    {visibleFlags.map((snap) => (
                        <VC.ToolButton
                            key={snap.flag}
                            className={c(VT.toolButton.button, styles.toolButton)}
                            src={snap.icon}
                            onSelect={() => toggleFlag(snap.flag)}
                            selected={hasFlag(snap.flag)}
                            multiSelect={true}
                            disabled={false}
                            focusKey={VF.FOCUS_DISABLED}
                            tooltip={snap.label}
                        />
                    ))}
                </div>
            </div>
        </div>
    );
};
