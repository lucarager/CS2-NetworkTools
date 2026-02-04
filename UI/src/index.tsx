import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import { GAME_BINDINGS } from "gameBindings";
import { initialize } from "components/vanilla/Components";
import { Wrapper } from "components/wrapper/wrapper";
import { TooltipRenderer } from "components/tooltipRenderer/tooltipRenderer";
import { Editor } from "components/editor/editor";

// Register bindings
GAME_BINDINGS.UI_DATA;

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    initialize(moduleRegistry);

    moduleRegistry.append("Game", TooltipRenderer);
    moduleRegistry.append("GameTopLeft", Wrapper);
    moduleRegistry.append("Editor", Editor);
};

export default register;
