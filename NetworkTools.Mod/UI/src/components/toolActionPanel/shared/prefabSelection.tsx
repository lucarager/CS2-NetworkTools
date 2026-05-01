import React from "react";
import styles from "../toolActionPanel.module.scss";
import { GAME_BINDINGS } from "gameBindings";
import { useValue } from "cs2/api";
import { usePrefabSearch } from "../prefabSearchPanel/prefabSearchContext";
import { useLocalization } from "cs2/l10n";

export const PrefabSelection: React.FC = () => {
    const selectedNetPrefab = useValue(GAME_BINDINGS.SELECTED_NET_PREFAB.binding);
    const { isOpen, open, close } = usePrefabSearch();
    const { translate } = useLocalization();
    return (
        <>
            <div className={styles.divider}></div>
            <div className={styles.col}>
                <div className={styles.controlRow}>
                    <div className={styles.controlRowInner}>
                        <span className={styles.paramLabel}>{translate("NetworkTools.UI.PrefabSearch.NetworkPrefab")}</span>
                        <button
                            className={styles.entityPreview}
                            onClick={() => (isOpen ? close() : open())}>
                            <img
                                src={selectedNetPrefab.Thumbnail}
                                className={styles.entityPreview__thumbnail}
                            />
                            <span className={styles.entityPreview__name}>
                                {translate(
                                    `Assets.NAME[${selectedNetPrefab.Name}]`,
                                    selectedNetPrefab.Name,
                                )}
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
