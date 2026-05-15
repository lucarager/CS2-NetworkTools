import React from "react";
import styles from "./tooltipRenderer.module.scss";
import { bindValue, useValue } from "cs2/api";
import { VC, VT } from "components/vanilla/Components";
import { Alignment, TooltipGroup } from "components/vanilla/types";
import { Unit } from "cs2/l10n";
import { c } from "utils/classes";

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

type SlopeTooltipProps = {
    CurrentSlope: number;
    NewSlope: number;
};

const customTooltipComponents: { [key: string]: React.FC<{ props: SlopeTooltipProps }> } = {
    "NT.SlopeTooltip": ({ props }) => {
        return (
            <div className={c(VT.tooltipRenderer.item, styles.slopeTooltip)}>
                <div className={styles.slopeTooltip__icon}>
                    <img src="Media/Glyphs/Slope.svg" />
                </div>
                <div className={styles.slopeTooltip__value}>
                    <VC.LocalizedNumber
                        value={props.CurrentSlope}
                        unit={Unit.PercentageSingleFraction}
                    />
                </div>
                {!isNaN(props.NewSlope) && (
                    <>
                        <div className={styles.slopeTooltip__arrow}>{"→"}</div>
                        <div
                            className={c(
                                styles.slopeTooltip__value,
                                styles.slopeTooltip__value__new,
                                VT.tooltipRenderer.success,
                            )}>
                            <VC.LocalizedNumber
                                value={props.NewSlope}
                                unit={Unit.PercentageSingleFraction}
                            />
                        </div>
                    </>
                )}
            </div>
        );
    },
};

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
                                (VC.tooltipComponents[tooltip.props.__Type] &&
                                    React.createElement(
                                        VC.tooltipComponents[tooltip.props.__Type],
                                        {
                                            props: tooltip.props,
                                        },
                                    )) ||
                                (customTooltipComponents[tooltip.props.__Type] &&
                                    React.createElement(
                                        customTooltipComponents[tooltip.props.__Type],
                                        {
                                            props: tooltip.props as any,
                                        },
                                    )),
                        )}
                    </div>
                </div>
            ))}
        </div>
    );
};
