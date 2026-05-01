import React, { useState } from "react";
import { ToolActionPanel } from "components/toolActionPanel/toolActionPanel";
import { ToolSelectPanel } from "components/toolSelectPanel/toolSelectPanel";
import { Button, Tooltip } from "cs2/ui";
import styles from "./editor.module.scss";
import { useLocalization } from "cs2/l10n";

export const Editor = () => {
    const [enabled, setIsEnabled] = useState(false);
    const { translate } = useLocalization();

    return (
        <>
            <div className={styles.buttonWrapper}>
                <Tooltip tooltip={translate("NetworkTools.UI.Common.NetworkTools")} delayTime={0} direction="down">
                    <Button
                        variant="floating"
                        onSelect={() => setIsEnabled(!enabled)}
                        src={"coui://nt/Logo.svg"}
                    />
                </Tooltip>
            </div>
            <div className={styles.editorWrapper}>
                {enabled && <ToolSelectPanel />}
                {enabled && <ToolActionPanel />}
            </div>
        </>
    );
};
