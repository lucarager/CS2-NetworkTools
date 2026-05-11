import React from "react";
import styles from "../toolActionPanel.module.scss";
import { useValue, bindValue } from "cs2/api";
import { usePrefabSearch } from "../prefabSearchPanel/prefabSearchContext";
import { useLocalization } from "cs2/l10n";
import { EMPTY_NET_PREFAB_DATA, type NetPrefabData } from "gameBindings";
import mod from "mod.json";

const bindingCache = new Map<string, ReturnType<typeof bindValue<NetPrefabData>>>();

function getNetPrefabBinding(key: string) {
    if (!bindingCache.has(key)) {
        bindingCache.set(key, bindValue<NetPrefabData>(mod.id, `BINDING:${key}`, EMPTY_NET_PREFAB_DATA));
    }
    return bindingCache.get(key)!;
}

export const PrefabSelection: React.FC<{ paramKey: string }> = ({ paramKey }) => {
    const prefabData = useValue(getNetPrefabBinding(paramKey));
    const { isOpen, activeKey, open, close } = usePrefabSearch();
    const { translate } = useLocalization();
    const isThisOpen = isOpen && activeKey === paramKey;

    return (
        <>
            <div className={styles.divider}></div>
            <div className={styles.col}>
                <div className={styles.controlRow}>
                    <div className={styles.controlRowInner}>
                        <span className={styles.paramLabel}>
                            {translate("NetworkTools.UI.PrefabSearch.NetworkPrefab")}
                        </span>
                        <button
                            className={styles.entityPreview}
                            onClick={() => (isThisOpen ? close() : open(paramKey))}>
                            <img
                                src={prefabData.Thumbnail}
                                className={styles.entityPreview__thumbnail}
                            />
                            <div className={styles.entityPreview__name}>
                                {translate(
                                    `Assets.NAME[${prefabData.Name}]`,
                                    prefabData.Name,
                                )}
                            </div>
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
