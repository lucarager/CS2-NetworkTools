import React from "react";
import styles from "../toolActionPanel.module.scss";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import { ConnectMode, PARAM_BINDINGS } from "generated/parameters.generated";
import { useValue } from "cs2/api";
import { Button, Tooltip } from "cs2/ui";
import { PrefabSelection } from "../shared/prefabSelection";
import { c } from "utils/classes";
import { useLocalization } from "cs2/l10n";

const C = PARAM_BINDINGS.connect;

const CONNECT_MODES: { localeKey: string; id: ConnectMode; icon: string }[] = [
    {
        localeKey: "NetworkTools.UI.Connect.None",
        id: ConnectMode.None,
        icon: "coui://nt/Modes/Original.svg",
    },
    {
        localeKey: "NetworkTools.UI.Connect.SimpleCurve",
        id: ConnectMode.SimpleCurve,
        icon: "coui://nt/Modes/ConnectSimpleCurve.svg",
    },
    {
        localeKey: "NetworkTools.UI.Connect.ComplexCurve",
        id: ConnectMode.ComplexCurve,
        icon: "coui://nt/Modes/ConnectComplexCurve.svg",
    },
    {
        localeKey: "NetworkTools.UI.Connect.Loop",
        id: ConnectMode.Loop,
        icon: "coui://nt/Modes/ConnectLoop.svg",
    },
];

export const ConnectControls: React.FC = () => {
    const selectedEntitiesBinding = useValue(GAME_BINDINGS.SELECTED_ENTITIES.binding);
    const activeConnectMode = useValue(C.mode.binding) as ConnectMode;
    const { translate } = useLocalization();

    return (
        <>
            {/* <NodeSelection selectedEntities={selectedEntitiesBinding} /> */}
            <PrefabSelection />

            {/* Transform Controls - Show when 2+ nodes selected */}
            {selectedEntitiesBinding.length >= 2 && (
                <>
                    <div className={styles.divider}></div>
                    <div className={styles.col}>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>{translate("NetworkTools.UI.Common.Mode")}</span>
                                <div className={styles.buttonRow}>
                                    {CONNECT_MODES.map((mode) => (
                                        <Tooltip key={mode.id} tooltip={translate(mode.localeKey)} delayTime={0}>
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
                                                    C.mode.set(mode.id)
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
                        <span className={styles.helper}>{translate("NetworkTools.UI.Common.SelectAtLeastTwoNodes")}</span>
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
                            {translate("NetworkTools.UI.Connect.ApplyCurve")}
                        </Button>
                    )}
                </div>
            </div>
        </>
    );
};
