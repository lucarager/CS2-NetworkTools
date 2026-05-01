import React from "react";
import styles from "../toolActionPanel.module.scss";
import { ViewOption } from "gameBindings";
import { useViews } from "hooks/useViews";
import { VC, VF, VT } from "components/vanilla/Components";
import { c } from "utils/classes";
import { useLocalization } from "cs2/l10n";

/** Human-readable metadata for each view flag. */
const VIEW_FLAGS: { flag: ViewOption; localeKey: string; icon: string }[] = [
    {
        flag: ViewOption.Underground,
        localeKey: "NetworkTools.UI.View.Underground",
        icon: "coui://nt/View/Underground.svg",
    },
    {
        flag: ViewOption.ZoneGrid,
        localeKey: "NetworkTools.UI.View.ZoneGrid",
        icon: "coui://nt/View/ZoneGrid.svg",
    },
    {
        flag: ViewOption.InvisibleNetworks,
        localeKey: "NetworkTools.UI.View.InvisibleNetworks",
        icon: "coui://nt/View/InvisibleNetworks.svg",
    },
];

export const ViewSelection: React.FC = () => {
    const { available, selected, setSelected, hasFlag, toggleFlag } = useViews();
    const { translate } = useLocalization();

    if (available === ViewOption.None) return null;

    const visibleFlags = VIEW_FLAGS.filter((f) => (available & f.flag) !== 0);
    const allSelected = (selected & available) === available;

    const handleToggleAll = () => {
        setSelected(allSelected ? ViewOption.None : available);
    };

    return (
        <div className={styles.controlRow}>
            <div className={styles.controlRowInner}>
                <span className={styles.paramLabel}>{translate("NetworkTools.UI.View.Label")}</span>
                <div className={styles.buttonRow}>
                    <VC.ToolButton
                        className={c(VT.toolButton.button, styles.iconButton)}
                        src={"coui://nt/View/All.svg"}
                        onSelect={handleToggleAll}
                        selected={allSelected}
                        multiSelect={true}
                        disabled={false}
                        focusKey={VF.FOCUS_DISABLED}
                        tooltip={translate("NetworkTools.UI.Common.ToggleAll")}
                    />
                    {visibleFlags.map((view) => (
                        <VC.ToolButton
                            key={view.flag}
                            className={c(VT.toolButton.button, styles.iconButton)}
                            src={view.icon}
                            onSelect={() => toggleFlag(view.flag)}
                            selected={hasFlag(view.flag)}
                            multiSelect={true}
                            disabled={false}
                            focusKey={VF.FOCUS_DISABLED}
                            tooltip={translate(view.localeKey)}
                        />
                    ))}
                </div>
            </div>
        </div>
    );
};
