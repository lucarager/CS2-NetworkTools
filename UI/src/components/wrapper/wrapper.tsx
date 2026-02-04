import React, { useState } from "react";
import styles from "./wrapper.module.scss";
import { ToolActionPanel } from "components/toolActionPanel/toolActionPanel";
import { ToolSelectPanel } from "components/toolSelectPanel/toolSelectPanel";
import { Button, Tooltip } from "cs2/ui";
import iconSrc from "../../assets/logo.svg";

export const Wrapper = () => {
    const [enabled, setIsEnabled] = useState(false);

    return (
        <>
            <Tooltip tooltip={`Network Tools`} delayTime={0} direction="down">
                <Button variant="floating" onSelect={() => setIsEnabled(!enabled)} src={iconSrc} />
            </Tooltip>
            <div className={styles.wrapper}>
                {enabled && <ToolSelectPanel />}
                {enabled && <ToolActionPanel />}
            </div>
        </>
    );
};
