import { ModRegistrar, ModuleRegistry } from "cs2/modding";
import { GAME_BINDINGS } from "gameBindings";
import { initialize } from "components/vanilla/Components";
import { TooltipRenderer } from "components/tooltipRenderer/tooltipRenderer";
import { EditorInjection } from "components/editorInjection/editorInjection";
import { GameInjection } from "components/gameInjection/gameInjection";

// Register bindings
GAME_BINDINGS.UI_DATA;

const register: ModRegistrar = (moduleRegistry: ModuleRegistry) => {
    initialize(moduleRegistry);

    moduleRegistry.append("Game", TooltipRenderer);
    moduleRegistry.append("GameTopLeft", GameInjection);
    moduleRegistry.append("Editor", EditorInjection);
};

export default register;
