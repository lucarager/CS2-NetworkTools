import React, { useState, useMemo } from "react";
import styles from "./prefabSearchPanel.module.scss";
import panels from "../../shared/panels.module.scss";
import { GAME_BINDINGS, GAME_TRIGGERS, PrefabType } from "gameBindings";
import { useValue } from "cs2/api";
import { c } from "utils/classes";
import { VC } from "components/vanilla/Components";

const PREFAB_TABS: { label: string; type: PrefabType }[] = [
    { label: "Road", type: PrefabType.Road },
    { label: "Path", type: PrefabType.Path },
    { label: "Rail", type: PrefabType.Rail },
    { label: "Waterway", type: PrefabType.Waterway },
    { label: "NetLane", type: PrefabType.NetLane },
];

type PrefabSearchPanelProps = {
    onClose: () => void;
};

export const PrefabSearchPanel: React.FC<PrefabSearchPanelProps> = ({ onClose }) => {
    const selectedType = useValue(GAME_BINDINGS.PS_SELECTED_TYPE.binding);
    const prefabData = useValue(GAME_BINDINGS.PS_DATA.binding);
    const [searchQuery, setSearchQuery] = useState("");

    const filteredPrefabs = useMemo(() => {
        if (!searchQuery.trim()) return prefabData;
        const query = searchQuery.toLowerCase();
        return prefabData.filter((p) => p.Name.toLowerCase().includes(query));
    }, [prefabData, searchQuery]);

    return (
        <div className={c(panels.nt_panel, styles.panel)}>
            <div className={styles.header}>
                <span className={styles.title}>Select Prefab</span>
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
                    placeholder="Search prefabs..."
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
                        {tab.label}
                    </button>
                ))}
            </div>

            <div className={styles.list}>
                <VC.Scrollable>
                    {filteredPrefabs.length === 0 && (
                        <div className={styles.empty}>No prefabs found.</div>
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
                            {prefab.Name}
                        </div>
                    ))}
                </VC.Scrollable>
            </div>
        </div>
    );
};
