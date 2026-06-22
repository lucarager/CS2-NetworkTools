import React from "react";
import { Button, Tooltip } from "cs2/ui";
import styles from "./editorInjection.module.scss";
import { useLocalization } from "cs2/l10n";
import { NetworkToolsWrapper } from "components/wrapper/wrapper";
import { useValue } from "cs2/api";
import { GAME_BINDINGS } from "gameBindings";

export const EditorInjection = () => {
    const panelOpenBinding = useValue(GAME_BINDINGS.PANEL_OPEN.binding);
    const { translate } = useLocalization();

    return (
        <>
            <div className={styles.buttonWrapper}>
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
            </div>
            <div className={styles.editorWrapper}>
                <NetworkToolsWrapper />
            </div>
        </>
    );
};
