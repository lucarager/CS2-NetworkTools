import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import { GAME_BINDINGS } from "gameBindings";
import { initialize } from "components/vanilla/Components";
import { Wrapper } from "components/wrapper/wrapper";

// Register bindings
GAME_BINDINGS.UI_DATA;

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    initialize(moduleRegistry);

    moduleRegistry.append("GameTopLeft", Wrapper);
};

export default register;
