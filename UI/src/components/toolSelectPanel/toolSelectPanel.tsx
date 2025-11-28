import React from "react";
import styles from "./toolSelectPanel.module.scss";
import { Button, Tooltip } from "cs2/ui";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import panels from "../shared/panels.module.scss";

export const ToolSelectPanel = () => {
    const toolUIDataBinding = useValue(GAME_BINDINGS.UI_DATA.binding);
    const selectedBinding = useValue(GAME_BINDINGS.SELECTED_PREFAB.binding);
    const { translate } = useLocalization();
    const activeIndex = toolUIDataBinding.findIndex((t) => t.ID === selectedBinding);

    return (
        <div className={[styles.wrapper, panels.nt_panel].join(" ")}>
            <div className={styles.row}>
                {toolUIDataBinding.map((tool, index) => {
                    return (
                        <Tooltip
                            key={index}
                            tooltip={tool.DisplayName}
                            delayTime={0}
                            direction="down">
                            <Button
                                className={[
                                    styles.actionButton,
                                    tool.ID == selectedBinding ? styles.actionButton__active : "",
                                ].join(" ")}
                                variant="flat"
                                onSelect={() => GAME_TRIGGERS.SELECT_TOOL(tool.ID)}>
                                <img
                                    src={`coui://nt/Icons/${tool.ID == selectedBinding ? "Active" : "Normal"}/${tool.Icon}`}
                                    className={styles.icon}
                                />
                            </Button>
                        </Tooltip>
                    );
                })}
                <div
                    className={styles.activeBar}
                    style={{
                        transform: `translateX(${activeIndex * 100}%)`,
                        opacity: activeIndex === -1 ? 0 : 1,
                    }}></div>
            </div>
        </div>
    );
};
