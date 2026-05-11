import React from "react";
import styles from "../toolActionPanel.module.scss";
import { GAME_BINDINGS, GAME_TRIGGERS } from "gameBindings";
import {
    ParallelSide,
    VerticalSide,
    PARAM_META,
    PARAM_KEYS,
    PARAM_BINDINGS,
} from "generated/parameters.generated";
import { useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { VC, VF, VT } from "components/vanilla/Components";
import { c } from "utils/classes";
import { PrefabSelection } from "../shared/prefabSelection";
import { useLocalization } from "cs2/l10n";

const P = PARAM_BINDINGS.parallel;
const hOffsetMeta = PARAM_META["parallel.horizontalOffset"];
const vOffsetMeta = PARAM_META["parallel.verticalOffset"];

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
    const selectedEntities = useValue(GAME_BINDINGS.SELECTED_ENTITIES.binding);
    const horizontalOffset = useValue(P.horizontalOffset.binding);
    const verticalOffset = useValue(P.verticalOffset.binding);
    const horizontalDirection = useValue(P.horizontalDirection.binding) as ParallelSide;
    const verticalDirection = useValue(P.verticalDirection.binding) as VerticalSide;
    const reverseDirection = useValue(P.reverseDirection.binding);
    const { translate } = useLocalization();

    return (
        <>
            {/* <NodeSelection selectedEntities={selectedEntities} /> */}
            <PrefabSelection paramKey={PARAM_KEYS.parallel.netPrefab} />

            {/* Configuration Controls - Show when 2+ nodes selected */}
            {selectedEntities.length >= 2 && (
                <>
                    <div className={styles.divider}></div>
                    <div className={styles.col}>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>
                                    {translate("NetworkTools.UI.Parallel.Side")}
                                </span>
                                <div className={styles.buttonRow}>
                                    {SIDE_OPTIONS.map((option) => (
                                        <VC.ToolButton
                                            key={option.id}
                                            tooltip={translate(option.localeKey)}
                                            className={c(VT.toolButton.button, styles.iconButton)}
                                            src={option.icon}
                                            onSelect={() => P.horizontalDirection.set(option.id)}
                                            selected={horizontalDirection === option.id}
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
                                    value={horizontalOffset}
                                    label={
                                        translate("NetworkTools.UI.Parallel.HorizontalOffset") ?? ""
                                    }
                                    min={hOffsetMeta.min}
                                    max={hOffsetMeta.max}
                                    fractionDigits={0}
                                    onChange={(e: number) => P.horizontalOffset.set(e)}
                                />
                            </div>
                        </div>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>
                                    {translate("NetworkTools.UI.Parallel.VerticalDirection")}
                                </span>
                                <div className={styles.buttonRow}>
                                    {VERTICAL_SIDE_OPTIONS.map((option) => (
                                        <VC.ToolButton
                                            key={option.id}
                                            tooltip={translate(option.localeKey)}
                                            className={c(VT.toolButton.button, styles.iconButton)}
                                            src={option.icon}
                                            onSelect={() => P.verticalDirection.set(option.id)}
                                            selected={verticalDirection === option.id}
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
                                    value={verticalOffset}
                                    label={
                                        translate("NetworkTools.UI.Parallel.VerticalOffset") ?? ""
                                    }
                                    min={vOffsetMeta.min}
                                    max={vOffsetMeta.max}
                                    fractionDigits={0}
                                    onChange={(e: number) => P.verticalOffset.set(e)}
                                />
                            </div>
                        </div>
                        <div className={styles.controlRow}>
                            <div className={styles.controlRowInner}>
                                <span className={styles.paramLabel}>
                                    {translate("NetworkTools.UI.Parallel.Direction")}
                                </span>
                                <div className={styles.buttonRow}>
                                    <VC.ToolButton
                                        tooltip={translate("NetworkTools.UI.Parallel.Same")}
                                        className={c(VT.toolButton.button, styles.iconButton)}
                                        src="coui://nt/Direction/Same.svg"
                                        onSelect={() => P.reverseDirection.set(false)}
                                        selected={!reverseDirection}
                                        multiSelect={false}
                                        disabled={false}
                                        focusKey={VF.FOCUS_DISABLED}
                                    />
                                    <VC.ToolButton
                                        tooltip={translate("NetworkTools.UI.Parallel.Reverse")}
                                        className={c(VT.toolButton.button, styles.iconButton)}
                                        src="coui://nt/Direction/Opposite.svg"
                                        onSelect={() => P.reverseDirection.set(true)}
                                        selected={reverseDirection}
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
                    {selectedEntities.length < 2 && (
                        <span className={styles.helper}>
                            {translate("NetworkTools.UI.Common.SelectAtLeastTwoNodes")}
                        </span>
                    )}
                    {selectedEntities.length >= 2 && (
                        <Button
                            variant="primary"
                            className={styles.applyButton}
                            disabled={selectedEntities.length < 2}
                            onSelect={() => GAME_TRIGGERS.REQUEST_APPLY()}>
                            {translate("NetworkTools.UI.Parallel.CreateParallel")}
                        </Button>
                    )}
                </div>
            </div>
        </>
    );
};
