import React from "react";
import styles from "../toolActionPanel.module.scss";
import { GAME_BINDINGS } from "gameBindings";
import { useValue } from "cs2/api";
import { usePrefabSearch } from "../prefabSearchPanel/prefabSearchContext";

export const PrefabSelection: React.FC = () => {
    const selectedNetPrefab = useValue(GAME_BINDINGS.SELECTED_NET_PREFAB.binding);
    const { isOpen, open, close } = usePrefabSearch();

    return (
        <>
            <div className={styles.divider}></div>
            <div className={styles.col}>
                <div className={styles.controlRow}>
                    <div className={styles.controlRowInner}>
                        <span className={styles.paramLabel}>Network Prefab</span>
                        <button
                            className={styles.entityPreview}
                            onClick={() => (isOpen ? close() : open())}>
                            <img
                                src={selectedNetPrefab.Thumbnail}
                                className={styles.entityPreview__thumbnail}
                            />
                            <span className={styles.entityPreview__name}>
                                {selectedNetPrefab.Name}
                            </span>
                            <img
                                src={"coui://uil/Standard/ArrowRightThickStroke.svg"}
                                className={styles.entityPreview__chevron}
                            />
                        </button>
                    </div>
                </div>
            </div>
        </>
    );
};
