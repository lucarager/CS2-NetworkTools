import React from "react";
import { Button, Tooltip } from "cs2/ui";
import styles from "./toolButton.module.scss";
import { useLocalization } from "cs2/l10n";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import { useValue } from "cs2/api";

export const buttonId = "networkToolBtn";
const iconSrc = "coui://uil/Standard/Road.svg";

export const ToolButton = () => {
    const [enabled, setIsEnabled] = React.useState(true);
    const { translate } = useLocalization();

    return (
        <>
            {enabled ? <ToolPanel /> : null}
            <Button variant="floating" onSelect={() => setIsEnabled(!enabled)} src={iconSrc} />
        </>
    );
};

export const ToolPanel = () => {
    const toolUIDataBinding = useValue(GAME_BINDINGS.UI_DATA.binding);
    const { translate } = useLocalization();

    return <></>;

    return (
        <div className={styles.panel}>
            <div className={styles.section}>
                <div className={styles.content}>
                    {toolUIDataBinding.map((tool, index) => {
                        return (
                            <Tooltip
                                key={index}
                                tooltip={tool.DisplayName}
                                delayTime={0}
                                direction={"right"}>
                                <Button
                                    className={styles.actionButton}
                                    variant="flat"
                                    onSelect={() => GAME_TRIGGERS.SELECT_TOOL(tool.ID)}>
                                    <img src={tool.Icon} className={styles.icon} />
                                </Button>
                            </Tooltip>
                        );
                    })}
                </div>
            </div>
        </div>
    );
};
