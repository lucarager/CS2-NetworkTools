import { useValue } from "cs2/api";
import { GAME_BINDINGS, TargetOption } from "../gameBindings";

/**
 * Hook for reading and writing target options from/to the active tool.
 *
 * @returns available - bitmask of target options the active tool supports
 * @returns selected - bitmask of target options currently enabled by the player
 * @returns setSelected - callback to update the selected target options
 * @returns hasFlag - helper to check if a specific target flag is selected
 * @returns toggleFlag - helper to toggle a specific target flag
 */
export function useTargets() {
    const available = useValue(GAME_BINDINGS.AVAILABLE_TARGETS.binding);
    const selected = useValue(GAME_BINDINGS.SELECTED_TARGETS.binding);

    const setSelected = (value: number) => {
        GAME_BINDINGS.SELECTED_TARGETS.set(value);
    };

    const hasFlag = (flag: TargetOption): boolean => (selected & flag) !== 0;

    const toggleFlag = (flag: TargetOption) => {
        setSelected(selected ^ flag);
    };

    return { available, selected, setSelected, hasFlag, toggleFlag } as const;
}
