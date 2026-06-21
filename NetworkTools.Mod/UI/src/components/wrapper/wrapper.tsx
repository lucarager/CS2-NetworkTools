import React from "react";
import { ToolActionPanel } from "components/toolActionPanel/toolActionPanel";
import { ToolSelectPanel } from "components/toolSelectPanel/toolSelectPanel";
import { useValue } from "cs2/api";
import { GAME_BINDINGS } from "gameBindings";

export const NetworkToolsWrapper = () => {
    const panelOpenBinding = useValue(GAME_BINDINGS.PANEL_OPEN.binding);

    if (!panelOpenBinding) {
        return null;
    }

    return (
        <>
            <ToolSelectPanel />
            <ToolActionPanel />
        </>
    );
};
