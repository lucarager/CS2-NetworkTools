import React from "react";
import styles from "../toolActionPanel.module.scss";
import {
    GAME_BINDINGS,
    GAME_TRIGGERS,
    ParallelConfigData,
    ParallelSide,
    VerticalSide,
} from "gameBindings";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { VC, VF, VT } from "components/vanilla/Components";
import { c } from "utils/classes";
import { PrefabSelection } from "../shared/prefabSelection";
import { useLocalization } from "cs2/l10n";

const SIDE_OPTIONS: { localeKey: string; id: ParallelSide; icon: string }[] = [
    {
        localeKey: "NetworkTools.UI.Parallel.Left",
        id: ParallelSide.Left,
        icon: "coui://nt/Side/Left.svg",
    },
    {
        localeKey: "NetworkTools.UI.Parallel.Right",
        id: ParallelSide.Right,
        icon: "coui://nt/Side/Right.svg",
    },
];

const VERTICAL_SIDE_OPTIONS: { localeKey: string; id: VerticalSide; icon: string }[] = [
    {
        localeKey: "NetworkTools.UI.Parallel.Up",
        id: VerticalSide.Up,
        icon: "coui://nt/Side/Up.svg",
    },
    {
        localeKey: "NetworkTools.UI.Parallel.Down",
        id: VerticalSide.Down,
        icon: "coui://nt/Side/Down.svg",
    },
];

export const ParallelControls: React.FC = () => {
    const selectedEntitiesBinding = useValue(GAME_BINDINGS.SELECTED_ENTITIES.binding);
    const parallelConfig = useValue(GAME_BINDINGS.PARALLEL_CONFIG.binding);
    const { translate } = useLocalization();

    const handleConfigChange = (param: keyof ParallelConfigData, value: number | boolean) => {
        const newConfig: ParallelConfigData = {
            ...parallelConfig,
            [param]: value,
        };

        console.log(newConfig);

        GAME_BINDINGS.PARALLEL_CONFIG.set(newConfig);
    };

    return (
        <>
            {/* <NodeSelection selectedEntities={selectedEntitiesBinding} /> */}
            <PrefabSelection />

            {/* Configuration Controls - Show when 2+ nodes selected */}
            {selectedEntitiesBinding.length >= 2 && (
                <>
                    <div className={styles.divider}></div>
                    <div className={styles.col}>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>{translate("NetworkTools.UI.Parallel.Side")}</span>
                                <div className={styles.buttonRow}>
                                    {SIDE_OPTIONS.map((option) => (
                                        <VC.ToolButton
                                            key={option.id}
                                            tooltip={translate(option.localeKey)}
                                            className={c(VT.toolButton.button, styles.iconButton)}
                                            src={option.icon}
                                            onSelect={() =>
                                                handleConfigChange("horizontalDirection", option.id)
                                            }
                                            selected={
                                                parallelConfig.horizontalDirection === option.id
                                            }
                                            multiSelect={false}
                                            disabled={false}
                                            focusKey={VF.FOCUS_DISABLED}
                                        />
                                    ))}
                                </div>
                            </div>
                        </div>
                        <div className={styles.controlRow}>
                            <div className={styles.sliderField}>
                                <VC.FloatSliderField
                                    value={parallelConfig.horizontalOffset}
                                    label={translate("NetworkTools.UI.Parallel.HorizontalOffset") ?? ""}
                                    min={0}
                                    max={80}
                                    fractionDigits={1}
                                    onChange={(e: number) => {
                                        console.log(e);
                                        handleConfigChange("horizontalOffset", e);
                                    }}
                                />
                            </div>
                        </div>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>{translate("NetworkTools.UI.Parallel.VerticalDirection")}</span>
                                <div className={styles.buttonRow}>
                                    {VERTICAL_SIDE_OPTIONS.map((option) => (
                                        <VC.ToolButton
                                            key={option.id}
                                            tooltip={translate(option.localeKey)}
                                            className={c(VT.toolButton.button, styles.iconButton)}
                                            src={option.icon}
                                            onSelect={() =>
                                                handleConfigChange("verticalDirection", option.id)
                                            }
                                            selected={
                                                parallelConfig.verticalDirection === option.id
                                            }
                                            multiSelect={false}
                                            disabled={false}
                                            focusKey={VF.FOCUS_DISABLED}
                                        />
                                    ))}
                                </div>
                            </div>
                        </div>
                        <div className={styles.controlRow}>
                            <div className={styles.sliderField}>
                                <VC.FloatSliderField
                                    value={parallelConfig.verticalOffset}
                                    label={translate("NetworkTools.UI.Parallel.VerticalOffset") ?? ""}
                                    min={0}
                                    max={80}
                                    fractionDigits={1}
                                    onChange={(e: number) => {
                                        console.log(e);
                                        handleConfigChange("verticalOffset", e);
                                    }}
                                />
                            </div>
                        </div>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>{translate("NetworkTools.UI.Parallel.Direction")}</span>
                                <div className={styles.buttonRow}>
                                    <VC.ToolButton
                                        tooltip={translate("NetworkTools.UI.Parallel.Same")}
                                        className={c(VT.toolButton.button, styles.iconButton)}
                                        src="coui://nt/Direction/Same.svg"
                                        onSelect={() =>
                                            handleConfigChange("reverseDirection", false)
                                        }
                                        selected={!parallelConfig.reverseDirection}
                                        multiSelect={false}
                                        disabled={false}
                                        focusKey={VF.FOCUS_DISABLED}
                                    />
                                    <VC.ToolButton
                                        tooltip={translate("NetworkTools.UI.Parallel.Reverse")}
                                        className={c(VT.toolButton.button, styles.iconButton)}
                                        src="coui://nt/Direction/Opposite.svg"
                                        onSelect={() =>
                                            handleConfigChange("reverseDirection", true)
                                        }
                                        selected={parallelConfig.reverseDirection}
                                        multiSelect={false}
                                        disabled={false}
                                        focusKey={VF.FOCUS_DISABLED}
                                    />
                                </div>
                            </div>
                        </div>
                    </div>
                </>
            )}

            {/* Primary Controls */}
            <div className={styles.divider}></div>
            <div className={styles.row}>
                <div className={styles.actions}>
                    {selectedEntitiesBinding.length < 2 && (
                        <span className={styles.helper}>{translate("NetworkTools.UI.Common.SelectAtLeastTwoNodes")}</span>
                    )}
                    {selectedEntitiesBinding.length >= 2 && (
                        <Button
                            variant="primary"
                            className={styles.applyButton}
                            disabled={selectedEntitiesBinding.length < 2}
                            onSelect={() => GAME_TRIGGERS.REQUEST_APPLY()}>
                            {translate("NetworkTools.UI.Parallel.CreateParallel")}
                        </Button>
                    )}
                </div>
            </div>
        </>
    );
};
