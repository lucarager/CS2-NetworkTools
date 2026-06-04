import { useValue } from "cs2/api";
import { GAME_BINDINGS } from "../gameBindings";

export function useAnarchy() {
    const available = useValue(GAME_BINDINGS.ANARCHY_AVAILABLE.binding);
    const enabled = useValue(GAME_BINDINGS.ANARCHY_ENABLED.binding);

    const setEnabled = (value: boolean) => {
        GAME_BINDINGS.ANARCHY_ENABLED.set(value);
    };

    const toggle = () => setEnabled(!enabled);

    return { available, enabled, toggle } as const;
}
