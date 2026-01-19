import React from "react";
import styles from "./tooltipRenderer.module.scss";
import { bindValue, useValue } from "cs2/api";
import { VC } from "components/vanilla/Components";
import { TooltipGroup } from "components/vanilla/types";

const tooltipGroups$ = bindValue<TooltipGroup[]>("NT_tooltip", "groups", []);

export const TooltipRenderer = () => {
    const tooltipGroups = useValue(tooltipGroups$);

    return (
        <div className={styles.wrapper}>
            {tooltipGroups.map((group, index) => (
                <div
                    key={index}
                    className={styles.tooltipGroup}
                    style={{
                        transform: `translate(${group.props.position.x}px, ${group.props.position.y}px)`,
                    }}>
                    {group.children.map(
                        (tooltip, tIndex) =>
                            VC.tooltipComponents[tooltip.props.__Type] &&
                            React.createElement(VC.tooltipComponents[tooltip.props.__Type], {
                                props: tooltip.props,
                            }),
                    )}
                </div>
            ))}
        </div>
    );
};
