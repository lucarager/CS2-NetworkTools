import React from "react";
import styles from "../toolActionPanel.module.scss";
import {
    GAME_BINDINGS,
    GAME_TRIGGERS,
    ParallelConfigData,
    ParallelSide,
    VerticalSide,
} from "gameBindings";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { NodeSelection } from "../shared/nodeSelection";
import { VC, VF, VT } from "components/vanilla/Components";
import { c } from "utils/classes";

const SIDE_OPTIONS: { label: string; id: ParallelSide; icon: string }[] = [
    {
        label: "Left",
        id: ParallelSide.Left,
        icon: "coui://nt/Side/Left.svg",
    },
    {
        label: "Right",
        id: ParallelSide.Right,
        icon: "coui://nt/Side/Right.svg",
    },
];

const VERTICAL_SIDE_OPTIONS: { label: string; id: VerticalSide; icon: string }[] = [
    {
        label: "Up",
        id: VerticalSide.Up,
        icon: "coui://nt/Side/Up.svg",
    },
    {
        label: "Down",
        id: VerticalSide.Down,
        icon: "coui://nt/Side/Down.svg",
    },
];

export const ParallelControls: React.FC = () => {
    const selectedEntitiesBinding = useValue(GAME_BINDINGS.SELECTED_ENTITIES.binding);
    const parallelConfig = useValue(GAME_BINDINGS.PARALLEL_CONFIG.binding);
    const selectedNetPrefab = useValue(GAME_BINDINGS.SELECTED_NET_PREFAB.binding);

    const handleConfigChange = (param: keyof ParallelConfigData, value: number | boolean) => {
        const newConfig: ParallelConfigData = {
            ...parallelConfig,
            [param]: value,
        };

        console.log(newConfig);

        GAME_BINDINGS.PARALLEL_CONFIG.set(newConfig);
    };

    return (
        <>
            {/* <NodeSelection selectedEntities={selectedEntitiesBinding} /> */}

            {/* Configuration Controls - Show when 2+ nodes selected */}
            {selectedEntitiesBinding.length >= 2 && (
                <>
                    <div className={styles.divider}></div>
                    <div className={styles.col}>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>Network Prefab</span>
                                <div className={styles.entityPreview}>
                                    <img
                                        src={selectedNetPrefab.Thumbnail}
                                        className={styles.entityPreview__thumbnail}
                                    />
                                    <span className={styles.entityPreview__name}>
                                        {selectedNetPrefab.Name}
                                    </span>
                                </div>
                            </div>
                        </div>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>Side</span>
                                <div className={styles.buttonRow}>
                                    {SIDE_OPTIONS.map((option) => (
                                        <VC.ToolButton
                                            key={option.id}
                                            tooltip={option.label}
                                            className={c(VT.toolButton.button, styles.toolButton)}
                                            src={option.icon}
                                            onSelect={() =>
                                                handleConfigChange("horizontalDirection", option.id)
                                            }
                                            selected={
                                                parallelConfig.horizontalDirection === option.id
                                            }
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
                                    value={parallelConfig.horizontalOffset}
                                    label={"Horizontal Offset"}
                                    min={0}
                                    max={80}
                                    fractionDigits={1}
                                    onChange={(e: number) => {
                                        console.log(e);
                                        handleConfigChange("horizontalOffset", e);
                                    }}
                                />
                            </div>
                        </div>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>Vertical Direction</span>
                                <div className={styles.buttonRow}>
                                    {VERTICAL_SIDE_OPTIONS.map((option) => (
                                        <VC.ToolButton
                                            key={option.id}
                                            tooltip={option.label}
                                            className={c(VT.toolButton.button, styles.toolButton)}
                                            src={option.icon}
                                            onSelect={() =>
                                                handleConfigChange("verticalDirection", option.id)
                                            }
                                            selected={
                                                parallelConfig.verticalDirection === option.id
                                            }
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
                                    value={parallelConfig.verticalOffset}
                                    label={"Vertical Offset"}
                                    min={0}
                                    max={80}
                                    fractionDigits={1}
                                    onChange={(e: number) => {
                                        console.log(e);
                                        handleConfigChange("verticalOffset", e);
                                    }}
                                />
                            </div>
                        </div>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>Direction</span>
                                <div className={styles.buttonRow}>
                                    <VC.ToolButton
                                        tooltip="Same"
                                        className={c(VT.toolButton.button, styles.toolButton)}
                                        src="coui://nt/Direction/Same.svg"
                                        onSelect={() =>
                                            handleConfigChange("reverseDirection", false)
                                        }
                                        selected={!parallelConfig.reverseDirection}
                                        multiSelect={false}
                                        disabled={false}
                                        focusKey={VF.FOCUS_DISABLED}
                                    />
                                    <VC.ToolButton
                                        tooltip="Reverse"
                                        className={c(VT.toolButton.button, styles.toolButton)}
                                        src="coui://nt/Direction/Opposite.svg"
                                        onSelect={() =>
                                            handleConfigChange("reverseDirection", true)
                                        }
                                        selected={parallelConfig.reverseDirection}
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
                    {selectedEntitiesBinding.length < 2 && (
                        <span className={styles.helper}>Select at least two nodes.</span>
                    )}
                    {selectedEntitiesBinding.length >= 2 && (
                        <Button
                            variant="primary"
                            className={styles.applyButton}
                            disabled={selectedEntitiesBinding.length < 2}
                            onSelect={() => GAME_TRIGGERS.REQUEST_APPLY()}>
                            Create parallel network
                        </Button>
                    )}
                </div>
            </div>
        </>
    );
};
