import React from "react";
import styles from "./tooltipRenderer.module.scss";
import { bindValue, useValue } from "cs2/api";
import { VC } from "components/vanilla/Components";
import { Alignment, TooltipGroup } from "components/vanilla/types";

const tooltipGroups$ = bindValue<TooltipGroup[]>("NT_tooltip", "groups", []);

function GetAlignment(e: Alignment) {
    switch (e) {
        case Alignment.Start:
            return "flex-start";
        case Alignment.Center:
            return "center";
        case Alignment.End:
            return "flex-end";
        default:
            return "center";
    }
}

export const TooltipRenderer = () => {
    const tooltipGroups = useValue(tooltipGroups$);

    return (
        <div className={styles.wrapper}>
            {tooltipGroups.map((group, index) => (
                <div
                    key={group.path}
                    className={styles.tooltipGroupContainer}
                    style={{
                        transform: `translate(${group.props.position.x}px, ${group.props.position.y}px)`,
                        zIndex: group.path.endsWith("*") ? 9999 : 0,
                        justifyContent: GetAlignment(group.props.verticalAlignment),
                        alignItems: GetAlignment(group.props.horizontalAlignment),
                    }}>
                    <div
                        className={styles.tooltipGroup}
                        style={{ alignItems: GetAlignment(group.props.horizontalAlignment) }}>
                        {group.children.map(
                            (tooltip, tIndex) =>
                                VC.tooltipComponents[tooltip.props.__Type] &&
                                React.createElement(VC.tooltipComponents[tooltip.props.__Type], {
                                    props: tooltip.props,
                                }),
                        )}
                    </div>
                </div>
            ))}
        </div>
    );
};
