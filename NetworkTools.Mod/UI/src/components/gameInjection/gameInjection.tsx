import React, { useEffect } from "react";
import styles from "./gameInjection.module.scss";
import { Button, Tooltip } from "cs2/ui";
import { useValue } from "cs2/api";
import { GAME_BINDINGS } from "gameBindings";
import { useLocalization } from "cs2/l10n";
import { NetworkToolsWrapper } from "components/wrapper/wrapper";

export const GameInjection = () => {
    const panelOpenBinding = useValue(GAME_BINDINGS.PANEL_OPEN.binding);
    const { translate } = useLocalization();

    // Add class to body
    useEffect(() => {
        if (panelOpenBinding) {
            document.body.classList.add("nt__panelIsOpen");
        } else {
            document.body.classList.remove("nt__panelIsOpen");
        }

        return () => {
            document.body.classList.remove("nt__panelIsOpen");
        };
    }, [panelOpenBinding]);

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
                <NetworkToolsWrapper />
            </div>
        </>
    );
};
