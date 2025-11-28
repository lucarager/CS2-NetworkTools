import React, { useState } from "react";
import styles from "./wrapper.module.scss";
import { ToolActionPanel } from "components/toolActionPanel/toolActionPanel";
import { ToolExtraPanel } from "components/toolExtraPanel/toolExtraPanel";
import { ToolSelectPanel } from "components/toolSelectPanel/toolSelectPanel";
import { Button } from "cs2/ui";
const iconSrc = "coui://uil/Standard/Road.svg";

export const Wrapper = () => {
    const [enabled, setIsEnabled] = useState(true);

    return (
        <>
            <Button variant="floating" onSelect={() => setIsEnabled(!enabled)} src={iconSrc} />
            <div className={styles.wrapper}>
                {enabled && <ToolSelectPanel />}
                {enabled && <ToolActionPanel />}
                <div style={{ flex: 2 }}></div>
                {enabled && <ToolExtraPanel />}
            </div>
        </>
    );
};
