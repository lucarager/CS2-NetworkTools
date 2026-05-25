import React from "react";
import styles from "./wrapper.module.scss";
import { ToolActionPanel } from "components/toolActionPanel/toolActionPanel";
import { ToolSelectPanel } from "components/toolSelectPanel/toolSelectPanel";
import { Button, Tooltip } from "cs2/ui";
import { useValue } from "cs2/api";
import { GAME_BINDINGS } from "gameBindings";
import { useLocalization } from "cs2/l10n";

export const Wrapper = () => {
    const panelOpenBinding = useValue(GAME_BINDINGS.PANEL_OPEN.binding);
    const { translate } = useLocalization();

    return (
        <>
            <Tooltip
                tooltip={translate("NetworkTools.UI.Common.NetworkTools")}
                delayTime={0}
                direction="down">
                <Button
                    variant="floating"
                    onSelect={() => GAME_BINDINGS.PANEL_OPEN.set(!panelOpenBinding)}
                    src={"coui://nt/Logo.svg"}
                />
            </Tooltip>
            <div className={styles.wrapper}>
                {panelOpenBinding && <ToolSelectPanel />}
                {panelOpenBinding && <ToolActionPanel />}
            </div>
        </>
    );
};
