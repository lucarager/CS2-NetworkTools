import React from "react";
import styles from "../toolActionPanel.module.scss";
import { useValue, bindValue } from "cs2/api";
import { usePrefabSearch } from "../prefabSearchPanel/prefabSearchContext";
import { useLocalization } from "cs2/l10n";
import { EMPTY_NET_PREFAB_DATA, GAME_TRIGGERS, type NetPrefabData } from "gameBindings";
import { PARAM_META } from "generated/parameters.generated";
import mod from "mod.json";

const bindingCache = new Map<string, ReturnType<typeof bindValue<NetPrefabData>>>();

export function getNetPrefabBinding(key: string) {
    if (!bindingCache.has(key)) {
        bindingCache.set(
            key,
            bindValue<NetPrefabData>(mod.id, `BINDING:${key}`, EMPTY_NET_PREFAB_DATA),
        );
    }
    return bindingCache.get(key)!;
}

export function hasNetPrefabSelection(data: NetPrefabData): boolean {
    return data.Entity.index !== 0;
}

export const PrefabSelection: React.FC<{ paramKey: string }> = ({ paramKey }) => {
    const prefabData = useValue(getNetPrefabBinding(paramKey));
    const { isOpen, activeKey, open, close } = usePrefabSearch();
    const { translate } = useLocalization();
    const isThisOpen = isOpen && activeKey === paramKey;
    const hasSelection = hasNetPrefabSelection(prefabData);

    const meta = PARAM_META[paramKey as keyof typeof PARAM_META];
    const nullable = meta && "nullable" in meta && (meta as { nullable?: boolean }).nullable === true;
    const label = (meta && "label" in meta)
        ? translate(meta.label as string)
        : translate("NetworkTools.UI.PrefabSearch.NetworkPrefab");

    const displayName = hasSelection
        ? (translate(`Assets.NAME[${prefabData.Name}]`, prefabData.Name) ?? prefabData.Name)
        : (translate("NetworkTools.UI.PrefabSearch.None") ?? "None");

    return (
        <div className={styles.controlRow}>
            <div className={styles.controlRowInner}>
                <span className={styles.paramLabel}>{label}</span>
                <div className={styles.prefabSelectGroup}>
                    <button
                        className={styles.entityPreview}
                        onClick={() => (isThisOpen ? close() : open(paramKey))}>
                        {hasSelection && (
                            <img src={prefabData.Thumbnail} className={styles.entityPreview__thumbnail} />
                        )}
                        <div className={styles.entityPreview__name}>{displayName}</div>
                        <img
                            src={"coui://uil/Standard/ArrowRightThickStroke.svg"}
                            className={styles.entityPreview__chevron}
                        />
                    </button>
                    {nullable && hasSelection && (
                        <button
                            className={styles.prefabClearButton}
                            onClick={() => GAME_TRIGGERS.RESET_PARAM(paramKey)}>
                            <img
                                src={"coui://uil/Standard/XClose.svg"}
                                className={styles.prefabClearButton__icon}
                            />
                        </button>
                    )}
                </div>
            </div>
        </div>
    );
};
