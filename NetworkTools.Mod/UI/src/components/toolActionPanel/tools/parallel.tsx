import React from "react";
import styles from "../toolActionPanel.module.scss";
import {
    GAME_BINDINGS,
    GAME_TRIGGERS,
    ParallelSide,
    VerticalSide,
} from "gameBindings";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { VC, VF, VT } from "components/vanilla/Components";
import { c } from "utils/classes";
import { PrefabSelection } from "../shared/prefabSelection";
import { useLocalization } from "cs2/l10n";

const SIDE_OPTIONS: { localeKey: string; id: ParallelSide; icon: string }[] = [
    {
        localeKey: "NetworkTools.UI.Parallel.Left",
        id: ParallelSide.Left,
        icon: "coui://nt/Side/Left.svg",
    },
    {
        localeKey: "NetworkTools.UI.Parallel.Right",
        id: ParallelSide.Right,
        icon: "coui://nt/Side/Right.svg",
    },
];

const VERTICAL_SIDE_OPTIONS: { localeKey: string; id: VerticalSide; icon: string }[] = [
    {
        localeKey: "NetworkTools.UI.Parallel.Up",
        id: VerticalSide.Up,
        icon: "coui://nt/Side/Up.svg",
    },
    {
        localeKey: "NetworkTools.UI.Parallel.Down",
        id: VerticalSide.Down,
        icon: "coui://nt/Side/Down.svg",
    },
];

export const ParallelControls: React.FC = () => {
    const selectedEntities      = useValue(GAME_BINDINGS.SELECTED_ENTITIES.binding);
    const horizontalOffset      = useValue(GAME_BINDINGS.PARALLEL_HORIZONTAL_OFFSET.binding);
    const verticalOffset        = useValue(GAME_BINDINGS.PARALLEL_VERTICAL_OFFSET.binding);
    const horizontalDirection   = useValue(GAME_BINDINGS.PARALLEL_HORIZONTAL_DIRECTION.binding) as ParallelSide;
    const verticalDirection     = useValue(GAME_BINDINGS.PARALLEL_VERTICAL_DIRECTION.binding) as VerticalSide;
    const reverseDirection      = useValue(GAME_BINDINGS.PARALLEL_REVERSE_DIRECTION.binding);
    const { translate } = useLocalization();

    return (
        <>
            {/* <NodeSelection selectedEntities={selectedEntities} /> */}
            <PrefabSelection />

            {/* Configuration Controls - Show when 2+ nodes selected */}
            {selectedEntities.length >= 2 && (
                <>
                    <div className={styles.divider}></div>
                    <div className={styles.col}>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>{translate("NetworkTools.UI.Parallel.Side")}</span>
                                <div className={styles.buttonRow}>
                                    {SIDE_OPTIONS.map((option) => (
                                        <VC.ToolButton
                                            key={option.id}
                                            tooltip={translate(option.localeKey)}
                                            className={c(VT.toolButton.button, styles.iconButton)}
                                            src={option.icon}
                                            onSelect={() =>
                                                GAME_BINDINGS.PARALLEL_HORIZONTAL_DIRECTION.set(option.id)
                                            }
                                            selected={horizontalDirection === option.id}
                                            multiSelect={false}
                                            disabled={false}
                                            focusKey={VF.FOCUS_DISABLED}
                                        />
                                    ))}
                                </div>
                            </div>
                        </div>
                        <div className={styles.controlRow}>
                            <div className={styles.sliderField}>
                                <VC.FloatSliderField
                                    value={horizontalOffset}
                                    label={translate("NetworkTools.UI.Parallel.HorizontalOffset") ?? ""}
                                    min={0}
                                    max={80}
                                    fractionDigits={1}
                                    onChange={(e: number) =>
                                        GAME_BINDINGS.PARALLEL_HORIZONTAL_OFFSET.set(e)
                                    }
                                />
                            </div>
                        </div>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>{translate("NetworkTools.UI.Parallel.VerticalDirection")}</span>
                                <div className={styles.buttonRow}>
                                    {VERTICAL_SIDE_OPTIONS.map((option) => (
                                        <VC.ToolButton
                                            key={option.id}
                                            tooltip={translate(option.localeKey)}
                                            className={c(VT.toolButton.button, styles.iconButton)}
                                            src={option.icon}
                                            onSelect={() =>
                                                GAME_BINDINGS.PARALLEL_VERTICAL_DIRECTION.set(option.id)
                                            }
                                            selected={verticalDirection === option.id}
                                            multiSelect={false}
                                            disabled={false}
                                            focusKey={VF.FOCUS_DISABLED}
                                        />
                                    ))}
                                </div>
                            </div>
                        </div>
                        <div className={styles.controlRow}>
                            <div className={styles.sliderField}>
                                <VC.FloatSliderField
                                    value={verticalOffset}
                                    label={translate("NetworkTools.UI.Parallel.VerticalOffset") ?? ""}
                                    min={0}
                                    max={80}
                                    fractionDigits={1}
                                    onChange={(e: number) =>
                                        GAME_BINDINGS.PARALLEL_VERTICAL_OFFSET.set(e)
                                    }
                                />
                            </div>
                        </div>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>{translate("NetworkTools.UI.Parallel.Direction")}</span>
                                <div className={styles.buttonRow}>
                                    <VC.ToolButton
                                        tooltip={translate("NetworkTools.UI.Parallel.Same")}
                                        className={c(VT.toolButton.button, styles.iconButton)}
                                        src="coui://nt/Direction/Same.svg"
                                        onSelect={() =>
                                            GAME_BINDINGS.PARALLEL_REVERSE_DIRECTION.set(false)
                                        }
                                        selected={!reverseDirection}
                                        multiSelect={false}
                                        disabled={false}
                                        focusKey={VF.FOCUS_DISABLED}
                                    />
                                    <VC.ToolButton
                                        tooltip={translate("NetworkTools.UI.Parallel.Reverse")}
                                        className={c(VT.toolButton.button, styles.iconButton)}
                                        src="coui://nt/Direction/Opposite.svg"
                                        onSelect={() =>
                                            GAME_BINDINGS.PARALLEL_REVERSE_DIRECTION.set(true)
                                        }
                                        selected={reverseDirection}
                                        multiSelect={false}
                                        disabled={false}
                                        focusKey={VF.FOCUS_DISABLED}
                                    />
                                </div>
                            </div>
                        </div>
                    </div>
                </>
            )}

            {/* Primary Controls */}
            <div className={styles.divider}></div>
            <div className={styles.row}>
                <div className={styles.actions}>
                    {selectedEntities.length < 2 && (
                        <span className={styles.helper}>{translate("NetworkTools.UI.Common.SelectAtLeastTwoNodes")}</span>
                    )}
                    {selectedEntities.length >= 2 && (
                        <Button
                            variant="primary"
                            className={styles.applyButton}
                            disabled={selectedEntities.length < 2}
                            onSelect={() => GAME_TRIGGERS.REQUEST_APPLY()}>
                            {translate("NetworkTools.UI.Parallel.CreateParallel")}
                        </Button>
                    )}
                </div>
            </div>
        </>
    );
};
