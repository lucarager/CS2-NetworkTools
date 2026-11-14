import React from "react";
import styles from "../toolActionPanel.module.scss";
import { ConnectMode, LoopShape, PARAM_KEYS, PARAM_BINDINGS } from "generated/parameters.generated";
import { useValue } from "cs2/api";
import { PrefabSelection } from "../shared/prefabSelection";
import { ParameterField } from "../shared/parameterField";
import { TunnelField } from "../shared/tunnelField";
import { TabBar } from "../shared/tabBar";

const C = PARAM_BINDINGS.connect;

export const ConnectControls: React.FC = () => {
    const activeConnectMode = useValue(C.mode.binding) as ConnectMode;
    const activeLoopShape = useValue(C.loopShape.binding) as LoopShape;

    return (
        <>
            <div className={styles.section}>
                <div className={styles.section__tabs}>
                    <TabBar paramKey="connect.mode" />
                </div>
                <div className={styles.section__content}>
                    <PrefabSelection paramKey={PARAM_KEYS.connect.netPrefab} />
                    <TunnelField paramKey="connect.tunnel" />
                    {activeConnectMode === ConnectMode.Loop && (
                        <>
                            {/* The option labels say what each shape is, so the row title
                                ("loop shape") is redundant -- hide it. */}
                            <ParameterField paramKey="connect.loopShape" hideLabel />
                            {/* Height of the flyover deck: positive = viaduct, negative = underpass.
                                Only the stacked shape has a deck to raise, so the field is hidden for
                                the single-arc shapes -- there the profile is fully determined by the
                                two nodes and the design grade. */}
                            {activeLoopShape === LoopShape.Stacked && (
                                <ParameterField paramKey="connect.loopHeight" />
                            )}
                            {/* Design grade of the whole link: how much of it is spent climbing. */}
                            <ParameterField paramKey="connect.loopGrade" />
                        </>
                    )}
                </div>
            </div>
        </>
    );
};
