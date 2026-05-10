import React, { useState, useMemo } from "react";
import styles from "./prefabSearchPanel.module.scss";
import panels from "../../shared/panels.module.scss";
import { GAME_BINDINGS, GAME_TRIGGERS, PrefabType } from "gameBindings";
import { useValue } from "cs2/api";
import { c } from "utils/classes";
import { VC } from "components/vanilla/Components";
import { useLocalization } from "cs2/l10n";

const PREFAB_TABS: { localeKey: string; type: PrefabType }[] = [
    { localeKey: "NetworkTools.UI.PrefabTab.Road", type: PrefabType.Road },
    { localeKey: "NetworkTools.UI.PrefabTab.Path", type: PrefabType.Path },
    { localeKey: "NetworkTools.UI.PrefabTab.Rail", type: PrefabType.Rail },
    { localeKey: "NetworkTools.UI.PrefabTab.Waterway", type: PrefabType.Waterway },
    { localeKey: "NetworkTools.UI.PrefabTab.NetLane", type: PrefabType.NetLane },
];

type PrefabSearchPanelProps = {
    onClose: () => void;
};

export const PrefabSearchPanel: React.FC<PrefabSearchPanelProps> = ({ onClose }) => {
    const selectedType = useValue(GAME_BINDINGS.PS_SELECTED_TYPE.binding);
    const prefabData = useValue(GAME_BINDINGS.PS_DATA.binding);
    const [searchQuery, setSearchQuery] = useState("");
    const { translate } = useLocalization();

    const filteredPrefabs = useMemo(() => {
        if (!searchQuery.trim()) return prefabData;
        const query = searchQuery.toLowerCase();
        return prefabData.filter((p) =>
            translate(`Assets.NAME[${p.Name}]`, p.Name)?.toLowerCase().includes(query),
        );
    }, [prefabData, searchQuery, translate]);

    return (
        <div className={c(panels.nt_panel, styles.panel)}>
            <div className={styles.header}>
                <span className={styles.title}>
                    {translate("NetworkTools.UI.PrefabSearch.Title")}
                </span>
                <button className={styles.closeButton} onClick={onClose}>
                    <img
                        src={"coui://uil/Standard/XClose.svg"}
                        className={styles.closeButton__icon}
                    />
                </button>
            </div>

            <div className={styles.searchBar}>
                <input
                    type="text"
                    placeholder={translate("NetworkTools.UI.PrefabSearch.Placeholder") ?? ""}
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                />
            </div>

            <div className={styles.tabs}>
                {PREFAB_TABS.map((tab) => (
                    <button
                        key={tab.type}
                        className={c(
                            styles.tab,
                            selectedType === tab.type ? styles.tab__active : "",
                        )}
                        onClick={() => GAME_BINDINGS.PS_SELECTED_TYPE.set(tab.type)}>
                        {translate(tab.localeKey)}
                    </button>
                ))}
            </div>

            <div className={styles.list}>
                <VC.Scrollable>
                    {filteredPrefabs.length === 0 && (
                        <div className={styles.empty}>
                            {translate("NetworkTools.UI.PrefabSearch.Empty")}
                        </div>
                    )}
                    {filteredPrefabs.map((prefab) => (
                        <div
                            key={`${prefab.Entity.index}-${prefab.Entity.version}`}
                            className={styles.listItem}
                            onClick={() => {
                                GAME_TRIGGERS.PS_SELECT(prefab.Entity);
                                onClose();
                            }}>
                            <img src={prefab.Icon} className={styles.listItem__icon} />
                            {translate(`Assets.NAME[${prefab.Name}]`, prefab.Name)}
                        </div>
                    ))}
                </VC.Scrollable>
            </div>
        </div>
    );
};
