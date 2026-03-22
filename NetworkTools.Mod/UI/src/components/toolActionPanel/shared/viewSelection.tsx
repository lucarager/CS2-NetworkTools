import React from "react";
import styles from "../toolActionPanel.module.scss";
import { ViewOption } from "gameBindings";
import { useViews } from "hooks/useViews";
import { VC, VF, VT } from "components/vanilla/Components";
import { c } from "utils/classes";

/** Human-readable metadata for each view flag. */
const VIEW_FLAGS: { flag: ViewOption; label: string; icon: string }[] = [
    {
        flag: ViewOption.Underground,
        label: "Underground",
        icon: "coui://nt/View/Underground.svg",
    },
    {
        flag: ViewOption.ZoneGrid,
        label: "Zone Grid",
        icon: "coui://nt/View/ZoneGrid.svg",
    },
    {
        flag: ViewOption.InvisibleNetworks,
        label: "Invisible Networks",
        icon: "coui://nt/View/InvisibleNetworks.svg",
    },
];

export const ViewSelection: React.FC = () => {
    const { available, selected, setSelected, hasFlag, toggleFlag } = useViews();

    if (available === ViewOption.None) return null;

    const visibleFlags = VIEW_FLAGS.filter((f) => (available & f.flag) !== 0);
    const allSelected = (selected & available) === available;

    const handleToggleAll = () => {
        setSelected(allSelected ? ViewOption.None : available);
    };

    return (
        <div className={styles.controlRow}>
            <div className={styles.controlRowInner}>
                <span className={styles.paramLabel}>View</span>
                <div className={styles.buttonRow}>
                    <VC.ToolButton
                        className={c(VT.toolButton.button, styles.toolButton)}
                        src={"coui://nt/View/All.svg"}
                        onSelect={handleToggleAll}
                        selected={allSelected}
                        multiSelect={true}
                        disabled={false}
                        focusKey={VF.FOCUS_DISABLED}
                        tooltip={"Toggle All"}
                    />
                    {visibleFlags.map((view) => (
                        <VC.ToolButton
                            key={view.flag}
                            className={c(VT.toolButton.button, styles.toolButton)}
                            src={view.icon}
                            onSelect={() => toggleFlag(view.flag)}
                            selected={hasFlag(view.flag)}
                            multiSelect={true}
                            disabled={false}
                            focusKey={VF.FOCUS_DISABLED}
                            tooltip={view.label}
                        />
                    ))}
                </div>
            </div>
        </div>
    );
};
