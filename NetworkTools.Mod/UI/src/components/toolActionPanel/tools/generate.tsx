import React from "react";
import styles from "../toolActionPanel.module.scss";
import { GAME_TRIGGERS } from "gameBindings";
import {
    GenerateMode,
    PARAM_KEYS,
    PARAM_BINDINGS,
} from "generated/parameters.generated";
import { useValue } from "cs2/api";
import { Button, Tooltip } from "cs2/ui";
import { PrefabSelection } from "../shared/prefabSelection";
import { ParameterField } from "../shared/parameterField";
import { c } from "utils/classes";
import { useLocalization } from "cs2/l10n";

const G = PARAM_BINDINGS.generate;

const GENERATE_MODES: { localeKey: string; id: GenerateMode; icon: string }[] = [
    {
        localeKey: "NetworkTools.UI.Generate.Grid",
        id: GenerateMode.Grid,
        icon: "coui://nt/Modes/GenerateGrid.svg",
    },
    {
        localeKey: "NetworkTools.UI.Generate.Circle",
        id: GenerateMode.Circle,
        icon: "coui://nt/Modes/GenerateCircle.svg",
    },
];

export const GenerateControls: React.FC = () => {
    const activeGenerateMode = useValue(G.mode.binding) as GenerateMode;
    const { translate } = useLocalization();

    return (
        <>
            <PrefabSelection paramKey={PARAM_KEYS.generate.netPrefab} />
            <div className={styles.divider}></div>
            <div className={styles.col}>
                <div className={styles.controlRow}>
                    <div className={styles.controlRowInner}>
                        <span className={styles.paramLabel}>
                            {translate("NetworkTools.UI.Common.Mode")}
                        </span>
                        <div className={styles.buttonRow}>
                            {GENERATE_MODES.map((mode) => (
                                <Tooltip
                                    key={mode.id}
                                    tooltip={translate(mode.localeKey)}
                                    delayTime={0}>
                                    <Button
                                        key={mode.id}
                                        variant="primary"
                                        className={c(
                                            styles.iconButton,
                                            activeGenerateMode === mode.id
                                                ? styles.iconButton__active
                                                : null,
                                        )}
                                        onSelect={() => G.mode.set(mode.id)}>
                                        <img src={mode.icon} className={styles.icon} />
                                    </Button>
                                </Tooltip>
                            ))}
                        </div>
                    </div>
                </div>
            </div>
            <div className={styles.divider}></div>
            <div className={styles.col}>
                {activeGenerateMode === GenerateMode.Grid && (
                    <>
                        <ParameterField paramKey="generate.gridXSpacing" />
                        <ParameterField paramKey="generate.gridZSpacing" />
                        <ParameterField paramKey="generate.gridXNum" />
                        <ParameterField paramKey="generate.gridZNum" />
                    </>
                )}
                {activeGenerateMode === GenerateMode.Circle && (
                    <ParameterField paramKey="generate.circleRadius" />
                )}
            </div>

            {/* Apply Button */}
            <div className={styles.divider}></div>
            <div className={styles.row}>
                <div className={styles.actions}>
                    <Button
                        variant="primary"
                        className={styles.applyButton}
                        onSelect={() => GAME_TRIGGERS.REQUEST_APPLY()}>
                        {translate("NetworkTools.UI.Generate.Apply")}
                    </Button>
                </div>
            </div>
        </>
    );
};
