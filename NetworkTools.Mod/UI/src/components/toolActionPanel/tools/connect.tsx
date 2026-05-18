import React, { useEffect } from "react";
import styles from "../toolActionPanel.module.scss";
import { ConnectMode, PARAM_KEYS, PARAM_BINDINGS } from "generated/parameters.generated";
import { useValue } from "cs2/api";
import { PrefabSelection } from "../shared/prefabSelection";
import { ParameterField } from "../shared/parameterField";
import { TabBar } from "../shared/tabBar";

const C = PARAM_BINDINGS.connect;

export const ConnectControls: React.FC<{
    toolId: number;
    onApplyDisabledChange?: (disabled: boolean) => void;
}> = ({ onApplyDisabledChange }) => {
    const activeConnectMode = useValue(C.mode.binding) as ConnectMode;

    useEffect(() => {
        onApplyDisabledChange?.(activeConnectMode === ConnectMode.None);
    }, [activeConnectMode, onApplyDisabledChange]);

    return (
        <>
            <div className={styles.section}>
                <div className={styles.section__tabs}>
                    <TabBar paramKey="connect.mode" />
                </div>
                <div className={styles.section__content}>
                    <PrefabSelection paramKey={PARAM_KEYS.connect.netPrefab} />
                    {activeConnectMode === ConnectMode.Loop && (
                        <ParameterField paramKey="connect.loopRadius" />
                    )}
                </div>
            </div>
        </>
    );
};
