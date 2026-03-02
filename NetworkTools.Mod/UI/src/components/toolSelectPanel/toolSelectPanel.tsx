import React, { useLayoutEffect, useMemo, useRef, useState } from "react";
import styles from "./toolSelectPanel.module.scss";
import { Button, Tooltip } from "cs2/ui";
import { useValue } from "cs2/api";
import { GAME_BINDINGS, GAME_TRIGGERS, ToolUIData } from "gameBindings";
import panels from "../shared/panels.module.scss";

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
    const columnRef = useRef<HTMLDivElement>(null);
    const [activeBarOffset, setActiveBarOffset] = useState(0);

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

    const hasActiveTool = useMemo(
        () => items.some((item) => item.type === "tool" && item.tool.PrefabId === selectedBinding),
        [items, selectedBinding],
    );

    useLayoutEffect(() => {
        const column = columnRef.current;
        if (!column) {
            return;
        }

        const activeToolElement = column.querySelector<HTMLElement>(
            `[data-prefab-id="${selectedBinding}"]`,
        );

        if (!activeToolElement) {
            setActiveBarOffset(0);
            return;
        }

        setActiveBarOffset(activeToolElement.offsetTop);
    }, [items, selectedBinding]);

    return (
        <div className={[styles.wrapper, panels.nt_panel].join(" ")}>
            <div className={styles.column} ref={columnRef}>
                {items.map((item) =>
                    item.type === "tool" ? (
                        <div key={item.key} data-prefab-id={item.tool.PrefabId}>
                            <Tooltip
                                tooltip={`${item.tool.DisplayName}${item.tool.Active ? "" : ` (Coming soon!)`}`}
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
                                    // disabled={!tool.Active}
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
                <div
                    className={styles.activeBar}
                    style={{
                        transform: `translateY(${activeBarOffset}rem)`,
                        opacity: hasActiveTool ? 1 : 0,
                    }}></div>
            </div>
        </div>
    );
};
