import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import { GAME_BINDINGS } from "gameBindings";
import { initialize } from "components/vanilla/Components";
import { ToolButton } from "components/toolButton/toolButton";
import { Toolbar } from "components/toolbar/toolbar";

// Register bindings
GAME_BINDINGS.UI_DATA;

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    initialize(moduleRegistry);

    moduleRegistry.append("GameTopLeft", ToolButton);
    moduleRegistry.append("Game", Toolbar);
};

export default register;
