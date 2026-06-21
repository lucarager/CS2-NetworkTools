import React, { useState } from "react";
import { Button, Tooltip } from "cs2/ui";
import styles from "./editorInjection.module.scss";
import { useLocalization } from "cs2/l10n";
import { NetworkToolsWrapper } from "components/wrapper/wrapper";

export const EditorInjection = () => {
    const [enabled, setIsEnabled] = useState(false);
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
                        onSelect={() => setIsEnabled(!enabled)}
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
