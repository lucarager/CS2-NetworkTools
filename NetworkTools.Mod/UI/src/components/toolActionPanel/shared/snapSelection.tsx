import React from "react";
import styles from "../toolActionPanel.module.scss";
import { SnapOption } from "gameBindings";
import { useSnap } from "hooks/useSnap";
import { VC, VF, VT } from "components/vanilla/Components";
import { c } from "utils/classes";
import { useLocalization } from "cs2/l10n";

/** Human-readable metadata for each snap flag. */
const SNAP_FLAGS: { flag: SnapOption; localeKey: string; icon: string }[] = [
    {
        flag: SnapOption.ZoneGrid,
        localeKey: "NetworkTools.UI.Snap.ZoneGrid",
        icon: "Media/Tools/Snap Options/ZoneGrid.svg",
    },
    {
        flag: SnapOption.MidPoint,
        localeKey: "NetworkTools.UI.Snap.MidPoint",
        icon: "coui://nt/Snap/MidPoint.svg",
    },
    {
        flag: SnapOption.ExistingGeometry,
        localeKey: "NetworkTools.UI.Snap.ExistingGeometry",
        icon: "Media/Tools/Snap Options/ExistingGeometry.svg",
    },
    {
        flag: SnapOption.ObjectSide,
        localeKey: "NetworkTools.UI.Snap.ObjectSide",
        icon: "Media/Tools/Snap Options/ObjectSide.svg",
    },
    {
        flag: SnapOption.GuideLines,
        localeKey: "NetworkTools.UI.Snap.GuideLines",
        icon: "Media/Tools/Snap Options/GuideLines.svg",
    },
];

export const SnapSelection: React.FC = () => {
    const { available, selected, setSelected, hasFlag, toggleFlag } = useSnap();
    const { translate } = useLocalization();

    if (available === SnapOption.None) return null;

    const visibleFlags = SNAP_FLAGS.filter((f) => (available & f.flag) !== 0);
    const allSelected = (selected & available) === available;

    const handleToggleAll = () => {
        setSelected(allSelected ? SnapOption.None : available);
    };

    return (
        <div className={styles.controlRow}>
            <div className={styles.controlRowInner}>
                <span className={styles.paramLabel}>{translate("NetworkTools.UI.Snap.Label")}</span>
                <div className={styles.buttonRow}>
                    <VC.ToolButton
                        className={c(VT.toolButton.button, styles.iconButton)}
                        src={"coui://nt/Snap/All.svg"}
                        onSelect={handleToggleAll}
                        selected={allSelected}
                        multiSelect={true}
                        disabled={false}
                        focusKey={VF.FOCUS_DISABLED}
                        tooltip={translate("NetworkTools.UI.Common.ToggleAll")}
                    />
                    {visibleFlags.map((snap) => (
                        <VC.ToolButton
                            key={snap.flag}
                            className={c(VT.toolButton.button, styles.iconButton)}
                            src={snap.icon}
                            onSelect={() => toggleFlag(snap.flag)}
                            selected={hasFlag(snap.flag)}
                            multiSelect={true}
                            disabled={false}
                            focusKey={VF.FOCUS_DISABLED}
                            tooltip={translate(snap.localeKey)}
                        />
                    ))}
                </div>
            </div>
        </div>
    );
};
