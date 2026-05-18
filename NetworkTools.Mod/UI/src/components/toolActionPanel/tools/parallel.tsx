import React from "react";
import styles from "../toolActionPanel.module.scss";
import { PARAM_KEYS } from "generated/parameters.generated";
import { PrefabSelection } from "../shared/prefabSelection";
import { ParameterField } from "../shared/parameterField";

export const ParallelControls: React.FC = () => {
    return (
        <div className={styles.section}>
            <div className={styles.section__content}>
                <PrefabSelection paramKey={PARAM_KEYS.parallel.netPrefab} />
                <ParameterField paramKey="parallel.reverseDirection" />
                {/* <ParameterField paramKey="parallel.origin" /> */}
                <ParameterField paramKey="parallel.horizontalOffset" />
                <ParameterField paramKey="parallel.verticalOffset" />
            </div>
        </div>
    );
};
