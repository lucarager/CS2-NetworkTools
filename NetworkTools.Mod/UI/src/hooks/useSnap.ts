import { useValue } from "cs2/api";
import { GAME_BINDINGS, SnapOption } from "../gameBindings";

/**
 * Hook for reading and writing snap options from/to the active tool.
 *
 * @returns available - bitmask of snap options the active tool supports
 * @returns selected - bitmask of snap options currently enabled by the player
 * @returns setSelected - callback to update the selected snap options
 * @returns hasFlag - helper to check if a specific snap flag is selected
 * @returns toggleFlag - helper to toggle a specific snap flag
 */
export function useSnap() {
    const available = useValue(GAME_BINDINGS.AVAILABLE_SNAPS.binding);
    const selected = useValue(GAME_BINDINGS.SELECTED_SNAPS.binding);

    const setSelected = (value: number) => {
        GAME_BINDINGS.SELECTED_SNAPS.set(value);
    };

    const hasFlag = (flag: SnapOption): boolean => (selected & flag) !== 0;

    const toggleFlag = (flag: SnapOption) => {
        setSelected(selected ^ flag);
    };

    return { available, selected, setSelected, hasFlag, toggleFlag } as const;
}
