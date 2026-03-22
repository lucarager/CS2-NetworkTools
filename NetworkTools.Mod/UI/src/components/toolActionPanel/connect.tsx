import React from "react";
import styles from "./toolActionPanel.module.scss";
import { ConnectMode, GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import { useValue } from "cs2/api";
import { Button, Tooltip } from "cs2/ui";
import { NodeSelection } from "./nodeSelection";
import { c } from "utils/classes";

const CONNECT_MODES: { label: string; id: ConnectMode; icon: string }[] = [
    {
        label: "None",
        id: ConnectMode.None,
        icon: "coui://nt/Modes/Original.svg",
    },
    {
        label: "Simple Curve",
        id: ConnectMode.SimpleCurve,
        icon: "coui://nt/Modes/ConnectSimpleCurve.svg",
    },
    {
        label: "Complex Curve",
        id: ConnectMode.ComplexCurve,
        icon: "coui://nt/Modes/ConnectComplexCurve.svg",
    },
    {
        label: "Loop",
        id: ConnectMode.Loop,
        icon: "coui://nt/Modes/ConnectLoop.svg",
    },
];

export const ConnectControls: React.FC = () => {
    const selectedEntitiesBinding = useValue(GAME_BINDINGS.SELECTED_ENTITIES.binding);
    const activeConnectMode = useValue(GAME_BINDINGS.CONNECT_MODE.binding);
    const selectedNetPrefab = useValue(GAME_BINDINGS.SELECTED_NET_PREFAB.binding);

    console.log("Selected Entities in ConnectControls:", selectedEntitiesBinding);

    return (
        <>
            <NodeSelection selectedEntities={selectedEntitiesBinding} />

            {/* Transform Controls - Show when 2+ nodes selected */}
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
                                <span className={styles.paramLabel}>Mode</span>
                                <div className={styles.buttonRow}>
                                    {CONNECT_MODES.map((mode) => (
                                        <Tooltip key={mode.id} tooltip={mode.label} delayTime={0}>
                                            <Button
                                                key={mode.id}
                                                variant="primary"
                                                className={c(
                                                    styles.iconButton,
                                                    activeConnectMode === mode.id
                                                        ? styles.iconButton__active
                                                        : null,
                                                )}
                                                onSelect={() =>
                                                    GAME_BINDINGS.CONNECT_MODE.set(mode.id)
                                                }>
                                                <img src={mode.icon} className={styles.icon} />
                                            </Button>
                                        </Tooltip>
                                    ))}
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
                            disabled={
                                selectedEntitiesBinding.length < 2 ||
                                activeConnectMode === ConnectMode.None
                            }
                            onSelect={() => GAME_TRIGGERS.REQUEST_APPLY()}>
                            Apply Curve
                        </Button>
                    )}
                </div>
            </div>
        </>
    );
};
