import React from "react";
import styles from "../toolActionPanel.module.scss";
import { useAnarchy } from "hooks/useAnarchy";
import { VC, VF, VT } from "components/vanilla/Components";
import { c } from "utils/classes";
import { useLocalization } from "cs2/l10n";

export const AnarchySelection: React.FC = () => {
    const { available, enabled, toggle } = useAnarchy();
    const { translate } = useLocalization();

    if (!available) return null;

    return (
        <div className={styles.controlRow}>
            <div className={styles.controlRowInner}>
                <span className={styles.paramLabel}>{translate("NetworkTools.UI.Anarchy.Label")}</span>
                <div className={styles.buttonRow}>
                    <VC.ToolButton
                        className={c(VT.toolButton.button, styles.iconButton)}
                        src={"coui://uil/Standard/Anarchy.svg"}
                        onSelect={toggle}
                        selected={enabled}
                        multiSelect={false}
                        disabled={false}
                        focusKey={VF.FOCUS_DISABLED}
                        tooltip={translate("NetworkTools.UI.Anarchy.Toggle")}
                    />
                </div>
            </div>
        </div>
    );
};
