import { useValue } from "cs2/api";
import { GAME_BINDINGS, ViewOption } from "../gameBindings";

/**
 * Hook for reading and writing view options from/to the active tool.
 *
 * @returns available - bitmask of view options the active tool supports
 * @returns selected - bitmask of view options currently enabled by the player
 * @returns setSelected - callback to update the selected view options
 * @returns hasFlag - helper to check if a specific view flag is selected
 * @returns toggleFlag - helper to toggle a specific view flag
 */
export function useViews() {
    const available = useValue(GAME_BINDINGS.AVAILABLE_VIEWS.binding);
    const selected = useValue(GAME_BINDINGS.SELECTED_VIEWS.binding);

    const setSelected = (value: number) => {
        GAME_BINDINGS.SELECTED_VIEWS.set(value);
    };

    const hasFlag = (flag: ViewOption): boolean => (selected & flag) !== 0;

    const toggleFlag = (flag: ViewOption) => {
        setSelected(selected ^ flag);
    };

    return { available, selected, setSelected, hasFlag, toggleFlag } as const;
}
