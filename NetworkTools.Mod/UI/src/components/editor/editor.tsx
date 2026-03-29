import React, { useState } from "react";
import { ToolActionPanel } from "components/toolActionPanel/toolActionPanel";
import { ToolSelectPanel } from "components/toolSelectPanel/toolSelectPanel";
import { Button, Tooltip } from "cs2/ui";
import styles from "./editor.module.scss";

export const Editor = () => {
    const [enabled, setIsEnabled] = useState(false);

    return (
        <>
            <div className={styles.buttonWrapper}>
                <Tooltip tooltip={`Network Tools`} delayTime={0} direction="down">
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
