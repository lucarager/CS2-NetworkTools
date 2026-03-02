import React from "react";
import styles from "./toolSelectPanel.module.scss";
import { Button, Tooltip } from "cs2/ui";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import panels from "../shared/panels.module.scss";

export const ToolSelectPanel = () => {
    const toolUIDataBinding = useValue(GAME_BINDINGS.UI_DATA.binding);
    const tools = toolUIDataBinding.sort((a, b) => a.Index - b.Index);
    const selectedBinding = useValue(GAME_BINDINGS.SELECTED_PREFAB.binding);
    const { translate } = useLocalization();
    const activeIndex = tools.findIndex((t) => t.PrefabId === selectedBinding);

    return (
        <div className={[styles.wrapper, panels.nt_panel].join(" ")}>
            <div className={styles.column}>
                {tools.map((tool, index) => {
                    return (
                        <Tooltip
                            key={index}
                            tooltip={`${tool.DisplayName}${tool.Active ? "" : ` (Coming soon!)`}`}
                            delayTime={0}
                            direction="right">
                            <Button
                                className={[
                                    styles.actionButton,
                                    tool.Active ? "" : styles.actionButton__inactive,
                                    tool.PrefabId == selectedBinding
                                        ? styles.actionButton__active
                                        : "",
                                ].join(" ")}
                                variant="flat"
                                disabled={!tool.Active}
                                onSelect={() => GAME_TRIGGERS.SELECT_TOOL(tool.PrefabId)}>
                                <img
                                    src={`coui://nt/Icons/${tool.PrefabId == selectedBinding ? "Active" : "Normal"}/${tool.Icon}`}
                                    className={styles.icon}
                                />
                            </Button>
                        </Tooltip>
                    );
                })}
                <div
                    className={styles.activeBar}
                    style={{
                        transform: `translateY(${activeIndex * 100}%)`,
                        opacity: activeIndex === -1 ? 0 : 1,
                    }}></div>
            </div>
        </div>
    );
};
