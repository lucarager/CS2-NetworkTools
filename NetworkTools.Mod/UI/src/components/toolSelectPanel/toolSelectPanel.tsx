import React, { useMemo } from "react";
import styles from "./toolSelectPanel.module.scss";
import { Button, Tooltip } from "cs2/ui";
import { useValue } from "cs2/api";
import { GAME_BINDINGS, GAME_TRIGGERS, ToolUIData } from "gameBindings";
import panels from "../shared/panels.module.scss";
import { useLocalization } from "cs2/l10n";

const CATEGORY_ORDER = ["node", "shape", "generative"];
type ToolListItem =
    | { type: "tool"; key: string; tool: ToolUIData }
    | { type: "divider"; key: string };

export const ToolSelectPanel = () => {
    const toolUIDataBinding = useValue(GAME_BINDINGS.UI_DATA.binding);
    const tools = useMemo(
        () => [...toolUIDataBinding].sort((a, b) => a.Index - b.Index),
        [toolUIDataBinding],
    );
    const selectedBinding = useValue(GAME_BINDINGS.SELECTED_PREFAB.binding);
    const { translate } = useLocalization();

    const items = useMemo(() => {
        const groups: { category: string; tools: ToolUIData[] }[] = [];
        for (const category of CATEGORY_ORDER) {
            const categoryTools = tools.filter((t) => t.Category === category);
            if (categoryTools.length > 0) {
                groups.push({ category, tools: categoryTools });
            }
        }

        const flatItems: ToolListItem[] = [];
        for (let groupIndex = 0; groupIndex < groups.length; groupIndex++) {
            const group = groups[groupIndex];

            for (const tool of group.tools) {
                flatItems.push({ type: "tool", key: tool.Id, tool });
            }

            if (groupIndex < groups.length - 1) {
                flatItems.push({
                    type: "divider",
                    key: `divider-${group.category}`,
                });
            }
        }

        return flatItems;
    }, [tools]);

    return (
        <div className={[styles.wrapper, panels.nt_panel].join(" ")}>
            <div className={styles.column}>
                {items.map((item) =>
                    item.type === "tool" ? (
                        <div key={item.key} className={styles.toolItem}>
                            <div
                                className={styles.activeIndicator}
                                style={{ opacity: item.tool.PrefabId === selectedBinding ? 1 : 0 }}
                            />
                            <Tooltip
                                tooltip={translate(item.tool.DisplayName)}
                                delayTime={0}
                                direction="right">
                                <Button
                                    className={[
                                        styles.actionButton,
                                        item.tool.Active ? "" : styles.actionButton__inactive,
                                        item.tool.PrefabId == selectedBinding
                                            ? styles.actionButton__active
                                            : "",
                                    ].join(" ")}
                                    variant="flat"
                                    disabled={!item.tool.Active}
                                    onSelect={() => GAME_TRIGGERS.SELECT_TOOL(item.tool.PrefabId)}>
                                    <img
                                        src={`coui://nt/Icons/${item.tool.PrefabId == selectedBinding ? "Active" : "Normal"}/${item.tool.Icon}`}
                                        className={styles.icon}
                                    />
                                </Button>
                            </Tooltip>
                        </div>
                    ) : (
                        <div key={item.key} className={styles.categoryDivider} />
                    ),
                )}
            </div>
        </div>
    );
};
